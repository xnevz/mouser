cmake_minimum_required(VERSION 3.20)
project(mouser_native LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include_directories(include)

if(APPLE)
    enable_language(OBJCXX)
    set(SOURCES
        src/platform_mac.mm
    )
    add_library(mouser_native SHARED ${SOURCES})
    target_link_libraries(mouser_native PRIVATE
        "-framework Foundation"
        "-framework ApplicationServices"
    )
    set_target_properties(mouser_native PROPERTIES
        MACOSX_RPATH TRUE
        OUTPUT_NAME "mouser_native"
    )
else()
    set(SOURCES
        src/platform_stub.cpp
    )
    add_library(mouser_native SHARED ${SOURCES})
endif()

# Output to common native bin folder
set_target_properties(mouser_native PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../../bin/native"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../../bin/native"
)
