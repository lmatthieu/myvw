//
// Created by matthieu on 10/19/15.
//

#include "parse_sframe.h"

#include <sys/types.h>

#ifndef _WIN32

#include <sys/mman.h>
#include <sys/wait.h>
#include <unistd.h>
#include <netinet/tcp.h>

#endif

#include <signal.h>

#include <fstream>
#include <boost/program_options.hpp>
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
#include <sstream>
#
#include <errno.h>
#include <stdio.h>
#include <assert.h>

#include "parse_example.h"
#include "cache.h"
#include "unique_sort.h"
#include "constant.h"
#include "vw.h"
#include "interactions.h"
#include "vw_exception.h"
#include "v_array.h"
#include "ezexample.h"

using namespace graphlab;

inline feature vw_feature_from_string(vw &v, string fstr, unsigned long seed, float val) {
    uint32_t foo = VW::hash_feature(v, fstr, seed);
    feature f = {val, foo};
    return f;
}

example *get_unused_example(vw &all);

void end_pass_example(vw &all, example *ae);

void mutex_lock(MUTEX *pm);

void mutex_unlock(MUTEX *pm);

void condition_variable_signal_all(CV *pcv);

char* copy(char* base);

#define GENBIN

using namespace std;

#ifdef _WIN32
DWORD WINAPI sf_main_parse_loop(LPVOID in)
#else

void *sf_main_parse_loop(void *in)
#endif
{
    vw *all = (vw *) in;
    size_t example_number = 0;  // for variable-size batch learning algorithms
    string sframe = all->sf_path;
    vector<string> &ns = all->sf_ns;
    string &meta = all->sf_meta;
    gl_sframe sf(sframe);
    map<string, vector<int> > namespaces;
    map<string, int> col_indices;
    bool end_it = false;
    int col_idx = 0;
    auto col_names = sf.column_names();
    auto col_types = sf.column_types();
    int target_col, id_col, w_col;
    example *ae;

    for (auto &col_name: sf.column_names()) {
        col_indices[col_name] = col_idx++;
    }
    for (const string &elt: ns) {
        vector<string> toks;

        boost::split(toks, elt, boost::is_any_of(":"));
        string ns_name = toks[0];

        toks.erase(toks.begin());

        for (auto &col_name: toks) {
            if (col_indices.find(col_name) != col_indices.end()) {
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

    cerr << "SFrame size " << sf.size() << " target_col " << target_col
    << " " << w_col << " " << id_col << endl;
    while (!all->p->done) {
        bool reseted = false;

        for (auto &row: sf.range_iterator()) {
            VW::primitive_feature_space features[namespaces.size()];
            uint32_t ns_hash[namespaces.size()];
            vector<feature> feats[namespaces.size()];
            size_t nidx = 0;
            stringstream ex;
            ae = get_unused_example(*all);

            for (auto &cur_ns: namespaces) {
                features[nidx].name = cur_ns.first[0];
                ns_hash[nidx] = VW::hash_space(*all, cur_ns.first);
                ++nidx;
            }
            float label = 0;

            if (target_col >= 0)
                ex << row[target_col] * 2. - 1.;
/*            if (w_col >= 0)
                label_str << " " << row[w_col];
            else
                label_str << " " << 1;
            if (id_col >= 0)
                label_str << " " << row[id_col];*/

#ifdef GENBIN
            ae->indices.erase();
            ae->total_sum_feat_sq = 0;
            ae->num_features = 0;
#endif

            nidx = 0;
            for (auto &cur_ns: namespaces) {
#ifdef GENBIN
                uint32_t index = cur_ns.first[0];
                int seed = ns_hash[nidx];
                ae->indices.push_back(index);
                ae->sum_feat_sq[index] = 0;
                ae->atomics[index].erase();
#else
                ex << " |" << cur_ns.first;
#endif
//                cout << "NS[" << cur_ns.second << "]";
                for (auto col_index: cur_ns.second) {
                    flexible_type value = row[col_index];

                    if (value.is_na() == false && value.is_zero() == false) {
                        if (col_types[col_index] == flex_type_enum::STRING) {
                            string cname = col_names[col_index] + "_" + value.to<string>();
#ifdef GENBIN
                            fid fint = VW::hash_feature(*all, cname, seed);
                            uint32_t word_hash = fint << all->reg.stride_shift;
                            feature f = {1., word_hash};
                            ae->atomics[index].push_back(f);
                            ae->sum_feat_sq[index] += 1.;
                            ae->total_sum_feat_sq += 1.;
                            ae->num_features++;

                            if (all->audit) {
                                v_array<char> feature_v = v_init<char>();
                                push_many(feature_v, cname.data(), cname.length());
                                feature_v.push_back('\0');
                                audit_data ad = {copy((char *)cur_ns.first.data()), feature_v.begin, word_hash, 1., true};
                                ae->audit_features[index].push_back(ad);
                            }
#else
                            ex << " " << cname;
#endif
                        }
                        else {
                            const string &cname = col_names[col_index];
                            float v = value.to<float>();
#ifdef GENBIN
                            fid fint = VW::hash_feature(*all, col_names[col_index], seed);
                            uint32_t word_hash = fint << all->reg.stride_shift;
                            feature f = {v, word_hash};
                            ae->atomics[index].push_back(f);
                            ae->sum_feat_sq[index] += v * v;
                            ae->total_sum_feat_sq += v * v;
                            ae->num_features++;

                            if (all->audit) {
                                v_array<char> feature_v = v_init<char>();
                                push_many(feature_v, cname.data(), cname.length());
                                feature_v.push_back('\0');
                                audit_data ad = {copy((char *)cur_ns.first.data()), feature_v.begin, word_hash, 1., true};
                                ae->audit_features[index].push_back(ad);
                            }
#else
                            ex << " " << col_names[col_index] << ":" << v;
#endif
                        }
                    }
                }
                ++nidx;
            }

            if (!all->do_reset_source && example_number != all->pass_length && all->max_examples > example_number) {
#ifdef GENBIN
                string label_str = ex.str();
                all->p->lp.default_label(&ae->l);

                if (label_str.length() > 0)
                    VW::parse_example_label(*all, *ae, label_str);
#else
                VW::read_line(*all, ae, (char*)ex.str().c_str());
#endif
                VW::parse_atomic_example(*all, ae, false);
                VW::setup_example(*all, ae);
                example_number++;
            }
            else {
                cout << "reset\n";
                reset_source(*all, all->num_bits);
                all->do_reset_source = false;
                all->passes_complete++;
                end_pass_example(*all, ae);
                if (all->passes_complete == all->numpasses && example_number == all->pass_length) {
                    all->passes_complete = 0;
                    all->pass_length = all->pass_length * 2 + 1;
                }
                if (all->passes_complete >= all->numpasses && all->max_examples >= example_number) {
                    mutex_lock(&all->p->examples_lock);
                    all->p->done = true;
                    mutex_unlock(&all->p->examples_lock);
                }
                example_number = 0;
                reseted = true;
            }

            if (reseted == false) {
                mutex_lock(&all->p->examples_lock);
                all->p->end_parsed_examples++;
                condition_variable_signal_all(&all->p->example_available);
                mutex_unlock(&all->p->examples_lock);
            } else
                break;
        }
        if (reseted == false) {
            ae = get_unused_example(*all);

            reset_source(*all, all->num_bits);
            all->do_reset_source = false;
            all->passes_complete++;
            end_pass_example(*all, ae);
            if (all->passes_complete == all->numpasses && example_number == all->pass_length) {
                all->passes_complete = 0;
                all->pass_length = all->pass_length * 2 + 1;
            }
            if (all->passes_complete >= all->numpasses && all->max_examples >= example_number) {
                mutex_lock(&all->p->examples_lock);
                all->p->done = true;
                mutex_unlock(&all->p->examples_lock);
            }
            example_number = 0;
        }
        mutex_lock(&all->p->examples_lock);
        all->p->end_parsed_examples++;
        condition_variable_signal_all(&all->p->example_available);
        mutex_unlock(&all->p->examples_lock);
    }
    return 0L;
}

