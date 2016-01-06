cmake_minimum_required(VERSION 2.8)

PROJECT(vowpal_wabbit)

FIND_PACKAGE(Boost 1.33.0 REQUIRED COMPONENTS program_options)
FIND_PACKAGE(ZLIB)
FIND_PACKAGE ( Threads REQUIRED )
EXECUTE_PROCESS ( COMMAND python -c "from distutils.sysconfig import get_python_lib; print get_python_lib()" OUTPUT_VARIABLE PYTHON_SITE_PACKAGES OUTPUT_STRIP_TRAILING_WHITESPACE)


set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11")

set(SOURCE_FILES
    vowpalwabbit/accumulate.cc
    vowpalwabbit/accumulate.h
    vowpalwabbit/active.cc
    vowpalwabbit/active.h
    vowpalwabbit/allreduce.h
    vowpalwabbit/allreduce_sockets.cc
    vowpalwabbit/allreduce_threads.cc
    vowpalwabbit/autolink.cc
    vowpalwabbit/autolink.h
    vowpalwabbit/beam.h
    vowpalwabbit/best_constant.cc
    vowpalwabbit/best_constant.h
    vowpalwabbit/bfgs.cc
    vowpalwabbit/bfgs.h
    vowpalwabbit/binary.cc
    vowpalwabbit/binary.h
    vowpalwabbit/boosting.cc
    vowpalwabbit/boosting.h
    vowpalwabbit/bs.cc
    vowpalwabbit/bs.h
    vowpalwabbit/cache.cc
    vowpalwabbit/cache.h
    vowpalwabbit/cb.cc
    vowpalwabbit/cb.h
    vowpalwabbit/cb_adf.cc
    vowpalwabbit/cb_adf.h
    vowpalwabbit/cb_algs.cc
    vowpalwabbit/cb_algs.h
    vowpalwabbit/cbify.cc
    vowpalwabbit/cbify.h
    vowpalwabbit/comp_io.cc
    vowpalwabbit/comp_io.h
    vowpalwabbit/config.h
    vowpalwabbit/constant.h
    vowpalwabbit/cost_sensitive.cc
    vowpalwabbit/cost_sensitive.h
    vowpalwabbit/csoaa.cc
    vowpalwabbit/csoaa.h
    vowpalwabbit/ect.cc
    vowpalwabbit/ect.h
    vowpalwabbit/example.cc
    vowpalwabbit/example.h
    vowpalwabbit/expreplay.h
    vowpalwabbit/ezexample.h
    vowpalwabbit/ftrl.cc
    vowpalwabbit/ftrl.h
    vowpalwabbit/gd.cc
    vowpalwabbit/gd.h
    vowpalwabbit/gd_mf.cc
    vowpalwabbit/gd_mf.h
    vowpalwabbit/global_data.cc
    vowpalwabbit/global_data.h
    vowpalwabbit/hash.cc
    vowpalwabbit/hash.h
    vowpalwabbit/interact.cc
    vowpalwabbit/interact.h
    vowpalwabbit/interactions.cc
    vowpalwabbit/interactions.h
    vowpalwabbit/io_buf.cc
    vowpalwabbit/io_buf.h
    vowpalwabbit/kernel_svm.cc
    vowpalwabbit/kernel_svm.h
    vowpalwabbit/label_dictionary.cc
    vowpalwabbit/label_dictionary.h
    vowpalwabbit/label_parser.h
    vowpalwabbit/lda_core.cc
    vowpalwabbit/lda_core.h
    vowpalwabbit/learner.cc
    vowpalwabbit/learner.h
    vowpalwabbit/log_multi.cc
    vowpalwabbit/log_multi.h
    vowpalwabbit/loss_functions.cc
    vowpalwabbit/loss_functions.h
    vowpalwabbit/lrq.cc
    vowpalwabbit/lrq.h
    vowpalwabbit/lrqfa.cc
    vowpalwabbit/lrqfa.h
    vowpalwabbit/memory.h
    vowpalwabbit/mf.cc
    vowpalwabbit/mf.h
    vowpalwabbit/multiclass.cc
    vowpalwabbit/multiclass.h
    vowpalwabbit/multilabel.cc
    vowpalwabbit/multilabel.h
    vowpalwabbit/multilabel_oaa.cc
    vowpalwabbit/multilabel_oaa.h
    vowpalwabbit/network.cc
    vowpalwabbit/network.h
    vowpalwabbit/nn.cc
    vowpalwabbit/nn.h
    vowpalwabbit/noop.cc
    vowpalwabbit/noop.h
    vowpalwabbit/oaa.cc
    vowpalwabbit/oaa.h
    vowpalwabbit/parse_args.cc
    vowpalwabbit/parse_args.h
    vowpalwabbit/parse_example.cc
    vowpalwabbit/parse_example.h
    vowpalwabbit/parse_primitives.cc
    vowpalwabbit/parse_primitives.h
    vowpalwabbit/parse_regressor.cc
    vowpalwabbit/parse_regressor.h
    vowpalwabbit/parser.cc
    vowpalwabbit/parser.h
    vowpalwabbit/print.cc
    vowpalwabbit/print.h
    vowpalwabbit/rand48.cc
    vowpalwabbit/rand48.h
    vowpalwabbit/reductions.h
    vowpalwabbit/scorer.cc
    vowpalwabbit/scorer.h
    vowpalwabbit/search.cc
    vowpalwabbit/search.h
    vowpalwabbit/search_dep_parser.cc
    vowpalwabbit/search_dep_parser.h
    vowpalwabbit/search_entityrelationtask.cc
    vowpalwabbit/search_entityrelationtask.h
    vowpalwabbit/search_graph.cc
    vowpalwabbit/search_graph.h
    vowpalwabbit/search_hooktask.cc
    vowpalwabbit/search_hooktask.h
    vowpalwabbit/search_meta.cc
    vowpalwabbit/search_meta.h
    vowpalwabbit/search_multiclasstask.cc
    vowpalwabbit/search_multiclasstask.h
    vowpalwabbit/search_sequencetask.cc
    vowpalwabbit/search_sequencetask.h
    vowpalwabbit/sender.cc
    vowpalwabbit/sender.h
    vowpalwabbit/simple_label.cc
    vowpalwabbit/simple_label.h
    vowpalwabbit/spanning_tree.cc
    vowpalwabbit/spanning_tree.h
    vowpalwabbit/stagewise_poly.cc
    vowpalwabbit/stagewise_poly.h
    vowpalwabbit/svrg.cc
    vowpalwabbit/svrg.h
    vowpalwabbit/topk.cc
    vowpalwabbit/topk.h
    vowpalwabbit/unique_sort.cc
    vowpalwabbit/unique_sort.h
    vowpalwabbit/v_array.h
    vowpalwabbit/v_hashmap.h
    vowpalwabbit/vw.h
    vowpalwabbit/vw_allreduce.h
    vowpalwabbit/vw_exception.cc
    vowpalwabbit/vw_exception.h
    vowpalwabbit/vwdll.cpp
    vowpalwabbit/vwdll.h
        vowpalwabbit/vw_validate.cc
        vowpalwabbit/active_cover.cc
        vowpalwabbit/gen_ns.cc vowpalwabbit/gen_ns.h vowpalwabbit/gen_ns.cc vowpalwabbit/gen_ns.h vowpalwabbit/parse_sframe.cc vowpalwabbit/parse_sframe.h)

INCLUDE_DIRECTORIES(vowpalwabbit)
INCLUDE_DIRECTORIES(${Boost_INCLUDE_DIRS})

ADD_LIBRARY(vw SHARED ${SOURCE_FILES})

TARGET_LINK_LIBRARIES(vw
        ${PYTHON_SITE_PACKAGES}/sframe/libadditional_sframe_utilities.so
        ${PYTHON_SITE_PACKAGES}/sframe/libgrouped_sframe.so
        ${PYTHON_SITE_PACKAGES}/sframe/libhdfs.so
        ${PYTHON_SITE_PACKAGES}/sframe/libinternal_demo.so
        ${PYTHON_SITE_PACKAGES}/sframe/libminipsutil.so
        ${PYTHON_SITE_PACKAGES}/sframe/libunity_shared.so)
TARGET_LINK_LIBRARIES(vw ${Boost_LIBRARIES}  ${ZLIB_LIBRARIES} ${CMAKE_THREAD_LIBS_INIT})
add_executable(vwexe vowpalwabbit/main.cc )
TARGET_LINK_LIBRARIES(vwexe vw)


