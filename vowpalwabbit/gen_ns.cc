//
// Created by matthieu on 10/12/15.
//
#ifdef SFRAME

#include <set>
#include <map>
#include <graphlab/flexible_type/flexible_type.hpp>
#include <graphlab/sdk/toolkit_function_macros.hpp>
#include <graphlab/sdk/toolkit_class_macros.hpp>
#include <graphlab/sdk/gl_sarray.hpp>
#include <graphlab/sdk/gl_sframe.hpp>
#include <graphlab/sdk/gl_sgraph.hpp>
#include <boost/algorithm/string/split.hpp>
#include <boost/algorithm/string/classification.hpp>
#include <boost/algorithm/string.hpp>

#include "gen_ns.h"

using namespace graphlab;


int gen_ns(string &sframe, vector<string> &ns, string &meta) {
    gl_sframe sf(sframe);
    map<string, vector<int> > namespaces;
    map<string, int> col_indices;
    bool end_it = false;
    int idx = 0;
    auto col_names = sf.column_names();
    auto col_types = sf.column_types();
    int target_col, id_col, w_col;

    for (auto &col_name: sf.column_names()) {
        col_indices[col_name] = idx++;
    }
    for (const string &elt: ns) {
        vector<string> toks;

        boost::split(toks, elt, boost::is_any_of(":"));
        string ns_name = toks[0];

        toks.erase(toks.begin());

        for (auto &col_name: toks) {
            if (col_indices.find(col_name) != col_indices.end()) {
                cerr << ns_name << " CNAME " << col_name << endl;
                namespaces[ns_name].push_back(col_indices[col_name]);
            }
        }
    }
    {
        vector<string> toks;
        boost::split(toks, meta, boost::is_any_of(":"));

        target_col = col_indices.find(toks[0]) != col_indices.end() ? col_indices[toks[0]] : -1;
        w_col = col_indices.find(toks[1]) != col_indices.end() ? col_indices[toks[1]] : -1;
        id_col = col_indices.find(toks[2]) != col_indices.end() ? col_indices[toks[2]] : -1;
    }
    cerr << "SFrame size " << sf.size() << endl;
    for (auto &row: sf.range_iterator()) {
        float label = 0;

        if (target_col >= 0)
          label = row[target_col] * 2. - 1.;
        cout << label << " ";

        for (auto &cur_ns: namespaces) {
            const string &ns_name = cur_ns.first;
            cout << "|" << cur_ns.first << " ";

                for (auto idx: cur_ns.second) {
                    flexible_type value = row[idx];

                    if (value.is_na() == false && value.is_zero() == false) {
                        cout << col_names[idx];

                        if (col_types[idx] == flex_type_enum::STRING)
                            cout << "_" << row[idx].to<string>();
                        else
                            cout << ":" << row[idx].to<float>();
                    }
                    cout << " ";
                }
        }
        cout << endl;
    }
    return 0;
}

#endif
