//
// Created by matthieu on 10/19/15.
//

#ifndef VOWPAL_WABBIT_PARSER_SFRAME_H
#define VOWPAL_WABBIT_PARSER_SFRAME_H

#ifdef _WIN32
DWORD WINAPI sf_main_parse_loop(LPVOID in);
#else
void *sf_main_parse_loop(void *in);
#endif

#endif //VOWPAL_WABBIT_PARSER_SFRAME_H
