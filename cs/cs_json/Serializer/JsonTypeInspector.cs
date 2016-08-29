﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AnnotationJsonInspector.cs">
//   Copyright (c) by respective owners including Yahoo!, Microsoft, and
//   individual contributors. All rights reserved.  Released under a BSD
//   license as described in the file LICENSE.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using VW.Labels;

namespace VW.Serializer
{
    /// <summary>
    /// Utility class analyzing compile-time <see cref="JsonPropertyAttribute"/> annotation.
    /// </summary>
    public static class JsonTypeInspector
    {
        /// <summary>
        /// The singleton native instance.
        /// </summary>
        public static readonly ITypeInspector Default = new JsonTypeInspectorImpl();

        private sealed class JsonTypeInspectorImpl : ITypeInspector
        {
            public Schema CreateSchema(VowpalWabbitSettings settings, Type type)
            {
                return JsonTypeInspector.CreateSchema(type, settings.PropertyConfiguration);
            }
        }

        private static readonly Type[] SupportedTypes;
        private static readonly Type[] DictTypes;

        static JsonTypeInspector()
        {
            var numericElementTypes = new[] { typeof(byte), typeof(sbyte), typeof(Int16), typeof(Int32), typeof(UInt16), typeof(UInt32), typeof(float), typeof(Int64), typeof(UInt64), typeof(double) };
            var enumerableType = typeof(IEnumerable<string>).GetGenericTypeDefinition();
            var dictType = typeof(IDictionary<string, int>).GetGenericTypeDefinition();

            SupportedTypes = new[]
            {
                typeof(char),
                typeof(bool),
                typeof(string),
                typeof(double),
                typeof(float),
                typeof(byte),
                typeof(decimal),
                typeof(UInt16),
                typeof(UInt32),
                typeof(UInt64),
                typeof(Int16),
                typeof(Int32),
                typeof(Int64),
            }
            .Union(numericElementTypes.Select(valueType => enumerableType.MakeGenericType(valueType)))
            .Union(numericElementTypes.Select(valueType => valueType.MakeArrayType()))
            .ToArray();

            DictTypes = numericElementTypes
                .Select(valueType => dictType.MakeGenericType(typeof(string), valueType))
                .ToArray();
        }

        private static bool IsTypeSupported(Type type)
        {
            return SupportedTypes.Any(t => t.IsAssignableFrom(type));
        }

        private static bool IsDictType(Type type)
        {
            return DictTypes.Any(t => t.IsAssignableFrom(type));
        }

        /// <summary>
        /// Extract the JSON.NET <see cref="MemberSerialization"/> from the type. Defaults to <see cref="MemberSerialization.OptOut"/>.
        /// </summary>
        /// <remarks><see cref="MemberSerialization.Fields"/> is not supported.</remarks>
        private static MemberSerialization GetMemberSerialiation(Type type)
        {
            var jsonObjectAttr = (JsonObjectAttribute)type.GetCustomAttributes(typeof(JsonObjectAttribute), true).FirstOrDefault();
            if (jsonObjectAttr == null)
                return MemberSerialization.OptOut;

            if (jsonObjectAttr.MemberSerialization == MemberSerialization.Fields)
                throw new ArgumentException("MemberSerialization.Fields is set on type " + type + " and is not supported");

            return jsonObjectAttr.MemberSerialization;
        }

        /// <summary>
        /// Extracts VW features from given type based on JSON.NET annotation. Basic structure:
        ///
        /// {
        ///   _label: { ... },  // SimpleLabel or ContextualBanditLabel
        ///   ns1: {            // Complex types denote namespaces. Property name becomes namespace name.
        ///     feature1: 5,    // Primitive types denote features
        ///     ...
        ///   },
        ///   ns2 : { ... },    // another namespace
        ///   feature2: true    // Top-level primitive property becomes feature in default namespace.
        /// }
        /// </summary>
        internal static Schema CreateSchema(Type type, PropertyConfiguration propertyConfiguration)
        {
            var exampleMemberSerialization = GetMemberSerialiation(type);

            // find all feature properties under namespace properties
            var namespaceFeatures =
                from ns in type.GetProperties()
                // removing any JsonIgnore properties
                where !ns.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any()
                let nsAttr = (JsonPropertyAttribute)ns.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault()
                where
                    !IsDictType(ns.PropertyType) &&  
                    !IsTypeSupported(ns.PropertyType) &&
                    // model OptIn/OptOut
                    (exampleMemberSerialization == MemberSerialization.OptOut || (exampleMemberSerialization == MemberSerialization.OptIn && nsAttr != null))
                let namespaceRawValue = nsAttr != null && nsAttr.PropertyName != null ? nsAttr.PropertyName : ns.Name
                // filter all aux properties
                where !namespaceRawValue.StartsWith(propertyConfiguration.FeatureIgnorePrefix, StringComparison.Ordinal)
                let featureGroup = namespaceRawValue[0]
                let namespaceValue = namespaceRawValue.Length > 1 ? namespaceRawValue.Substring(1) : null
                let namespaceMemberSerialization = GetMemberSerialiation(ns.PropertyType)
                from p in ns.PropertyType.GetProperties()
                // removing any JsonIgnore properties
                where !p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any()
                let attr = (JsonPropertyAttribute)p.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault()
                where IsTypeSupported(p.PropertyType) &&
                    // model OptIn/OptOut
                    (exampleMemberSerialization == MemberSerialization.OptOut || (exampleMemberSerialization == MemberSerialization.OptIn && attr != null))
                let name = attr != null && attr.PropertyName != null ? attr.PropertyName : p.Name
                let isTextProperty = name == propertyConfiguration.TextProperty
                // filter all aux properties
                where isTextProperty || !name.StartsWith(propertyConfiguration.FeatureIgnorePrefix, StringComparison.Ordinal)
                select new FeatureExpression(
                    featureType: p.PropertyType,
                    name: name,
                    // CODE example.NamespaceProperty.FeatureProperty
                    valueExpressionFactory: valueExpression => Expression.Property(Expression.Property(valueExpression, ns), p),
                    // Note: default to string escaping
                    stringProcessing: isTextProperty ? StringProcessing.Split : StringProcessing.EscapeAndIncludeName,
                    // CODE example != null
                    // CODE example.NamespaceProperty != null
                    valueValidExpressionFactories: new List<Func<Expression, Expression>>{
                        valueExpression => Expression.NotEqual(valueExpression, Expression.Constant(null)),
                        valueExpression => Expression.NotEqual(Expression.Property(valueExpression, ns), Expression.Constant(null))
                    },
                    @namespace: namespaceValue,
                    featureGroup: featureGroup);

            // find all top-level feature properties for the default namespace
            var defaultNamespaceFeatures =
                from p in type.GetProperties()
                // removing any JsonIgnore properties
                where !p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any()
                let attr = (JsonPropertyAttribute)p.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault()
                where
                    // model OptIn/OptOut
                    (exampleMemberSerialization == MemberSerialization.OptOut || (exampleMemberSerialization == MemberSerialization.OptIn && attr != null))
                let name = attr != null && attr.PropertyName != null ? attr.PropertyName : p.Name
                // filter all aux properties, except for special props
                where propertyConfiguration.IsSpecialProperty(name) ||
                   !name.StartsWith(propertyConfiguration.FeatureIgnorePrefix, StringComparison.Ordinal)
                // filtering labels for now
                where name != propertyConfiguration.LabelProperty
                where IsTypeSupported(p.PropertyType) ||
                    // _multi can be any list type that JSON.NET supports
                    name == propertyConfiguration.MultiProperty ||
                    // labels must be ILabel or string
                    // Note: from the JSON side they actually can be anything that serializes to the same properties as ILabel implementors
                    (name == propertyConfiguration.LabelProperty && (typeof(ILabel).IsAssignableFrom(p.PropertyType) || p.PropertyType == typeof(string)))
                select new FeatureExpression(
                    featureType: p.PropertyType,
                    name: name,
                    // CODE example.FeatureProperty
                    valueExpressionFactory: valueExpression => Expression.Property(valueExpression, p),
                    // Note: default to string escaping
                    stringProcessing: name == propertyConfiguration.TextProperty ? StringProcessing.Split : StringProcessing.EscapeAndIncludeName,
                    // CODE example != null
                    valueValidExpressionFactories: new List<Func<Expression, Expression>>{ valueExpression => Expression.NotEqual(valueExpression, Expression.Constant(null)) },
                    featureGroup: VowpalWabbitConstants.DefaultNamespace);

            // find all top-level dictionaries
            var topLevelDictionaries =
                from p in type.GetProperties()
                    // removing any JsonIgnore properties
                where !p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any()
                let attr = (JsonPropertyAttribute)p.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault()
                where
                    // model OptIn/OptOut
                    (exampleMemberSerialization == MemberSerialization.OptOut || (exampleMemberSerialization == MemberSerialization.OptIn && attr != null))
                where IsDictType(p.PropertyType)
                let name = attr != null && attr.PropertyName != null ? attr.PropertyName : p.Name
                let namespaceRawValue = attr != null && attr.PropertyName != null ? attr.PropertyName : p.Name
                // filter all aux properties
                where !namespaceRawValue.StartsWith(propertyConfiguration.FeatureIgnorePrefix, StringComparison.Ordinal)
                let featureGroup = namespaceRawValue[0]
                let namespaceValue = namespaceRawValue.Length > 1 ? namespaceRawValue.Substring(1) : null
                select new FeatureExpression(
                    featureType: p.PropertyType,
                    name: name,
                    // CODE example.FeatureProperty
                    valueExpressionFactory: valueExpression => Expression.Property(valueExpression, p),
                    // CODE example != null
                    valueValidExpressionFactories: new List<Func<Expression, Expression>> { valueExpression => Expression.NotEqual(valueExpression, Expression.Constant(null)) },
                    @namespace: namespaceValue,
                    featureGroup: featureGroup);

            // find label
            var labelProperties =
                from p in type.GetProperties()
                // removing any JsonIgnore properties
                where !p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any()
                let attr = (JsonPropertyAttribute)p.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault()
                where
                    // model OptIn/OptOut
                    (exampleMemberSerialization == MemberSerialization.OptOut || (exampleMemberSerialization == MemberSerialization.OptIn && attr != null))
                let name = attr != null && attr.PropertyName != null ? attr.PropertyName : p.Name
                // filtering labels for now
                where name == propertyConfiguration.LabelProperty
                where
                    // labels must be ILabel or string
                    // Note: from the JSON side they actually can be anything that serializes to the same properties as ILabel implementors
                    (name == propertyConfiguration.LabelProperty &&
                    (typeof(ILabel).IsAssignableFrom(p.PropertyType) || p.PropertyType == typeof(string)))
                select new LabelExpression
                {
                    LabelType = p.PropertyType,
                    Name = name,
                    // CODE example.Label
                    ValueExpressionFactory = valueExpression => Expression.Property(valueExpression, p),
                    // CODE example != null
                    ValueValidExpressionFactories = new List<Func<Expression, Expression>>{ valueExpression => Expression.NotEqual(valueExpression, Expression.Constant(null)) }
                };

            // TODO: _label_ and _labelIndex is not supported

            return new Schema
            {
                Label = labelProperties.FirstOrDefault(),
                Features = namespaceFeatures
                    .Union(defaultNamespaceFeatures)
                    .Union(topLevelDictionaries).ToList()
            };
        }
    }
}
