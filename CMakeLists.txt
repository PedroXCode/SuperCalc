cmake_minimum_required(VERSION 3.12)
project(SuperCalc LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(SuperCalc src/main.cpp)

if (MSVC)
  target_compile_options(SuperCalc PRIVATE /W4 /permissive-)
else()
  target_compile_options(SuperCalc PRIVATE -Wall -Wextra -Wpedantic)
endif()
