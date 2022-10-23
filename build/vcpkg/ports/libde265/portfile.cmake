# Used for .pc file
set(VERSION "1.0.9")
vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO strukturag/libde265
    REF 2b5dedd1a238267a59de0baaa91035f01194ce23 #v1.0.9+
    SHA512 223724746cf3d5fcfd7e80b15c6a202d2d0f7c136411687c85f491c8434b0edcd7f2b2ff773466e079aa6428315bbeddd5e2a85476be35a0b15dbee914e2028d
    HEAD_REF master
    PATCHES
        fix-libde265-headers.patch
)

if(NOT VCPKG_TARGET_ARCHITECTURE MATCHES "^x(86|64)$")
    set(DISABLE_SSE 1)
endif()

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DCMAKE_DISABLE_FIND_PACKAGE_SDL=ON
        -DENABLE_ENCODER=OFF
        -DDISABLE_SSE=${DISABLE_SSE}
)

vcpkg_cmake_install()
vcpkg_cmake_config_fixup(CONFIG_PATH lib/cmake/libde265)
vcpkg_copy_tools(TOOL_NAMES dec265 AUTO_CLEAN)

set(prefix "")
set(exec_prefix [[${prefix}]])
set(libdir [[${prefix}/lib]])
set(includedir [[${prefix}/include]])
set(LIBS "")
configure_file("${SOURCE_PATH}/libde265.pc.in" "${CURRENT_PACKAGES_DIR}/lib/pkgconfig/libde265.pc" @ONLY)
# The produced library name is `liblibde265.a` or `libde265.lib`
vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/lib/pkgconfig/libde265.pc" "-lde265" "-llibde265")
# libde265's pc file assumes libstdc++, which isn't always true.
vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/lib/pkgconfig/libde265.pc" " -lstdc++" "")
if(NOT VCPKG_BUILD_TYPE)
    configure_file("${SOURCE_PATH}/libde265.pc.in" "${CURRENT_PACKAGES_DIR}/debug/lib/pkgconfig/libde265.pc" @ONLY)
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/debug/lib/pkgconfig/libde265.pc" "-lde265" "-llibde265")
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/debug/lib/pkgconfig/libde265.pc" " -lstdc++" "")
endif()
vcpkg_fixup_pkgconfig()

if(VCPKG_LIBRARY_LINKAGE STREQUAL "static")
    file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/bin" "${CURRENT_PACKAGES_DIR}/debug/bin")
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libde265/de265.h" "!defined(LIBDE265_STATIC_BUILD)" "0")
else()
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libde265/de265.h" "!defined(LIBDE265_STATIC_BUILD)" "1")
endif()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(INSTALL "${SOURCE_PATH}/COPYING" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}" RENAME copyright)
