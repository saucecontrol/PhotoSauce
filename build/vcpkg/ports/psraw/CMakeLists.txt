cmake_minimum_required(VERSION 3.20)
project(psraw LANGUAGES CXX)

find_package(libraw CONFIG REQUIRED)

add_library(psraw SHARED psraw.cpp psraw.h)
target_compile_features(psraw PRIVATE cxx_std_17)
target_compile_definitions(psraw PRIVATE DLLDEFINE)
target_link_libraries(psraw PRIVATE libraw::raw_r)
set_target_properties(psraw PROPERTIES CXX_VISIBILITY_PRESET hidden)

if(UNIX AND NOT APPLE)
    target_link_options(psraw PRIVATE "-Wl,--version-script=${CMAKE_CURRENT_SOURCE_DIR}/psraw.ver")
endif()

include(GNUInstallDirs)
install(TARGETS psraw
    RUNTIME DESTINATION "${CMAKE_INSTALL_BINDIR}"
    LIBRARY DESTINATION "${CMAKE_INSTALL_LIBDIR}"
    ARCHIVE DESTINATION "${CMAKE_INSTALL_LIBDIR}"
)
install(FILES psraw.h DESTINATION include/psraw)
