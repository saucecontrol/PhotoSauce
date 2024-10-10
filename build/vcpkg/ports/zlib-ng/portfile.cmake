vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO zlib-ng/zlib-ng
    REF "${VERSION}"
    SHA512 3cb3e97ee1d20e1f3cdf0efcdf55aee0e3a192f9a2ae781cd209b1d37620c48f2ada345fb1f4357315b1cb5e09b7ea5fcdfa2fd54f7b4ac5dcb6e73860000aad
    HEAD_REF develop
)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DZLIB_COMPAT=ON
        -DZLIB_ENABLE_TESTS=OFF
        -DZLIBNG_ENABLE_TESTS=OFF
        -DWITH_GZFILEOP=OFF
        -DWITH_NEW_STRATEGIES=ON
        -DWITH_GTEST=OFF
    OPTIONS_RELEASE
        -DWITH_OPTIM=ON
        -DFORCE_SSE2=ON
    MAYBE_UNUSED_VARIABLES
        FORCE_SSE2
)
vcpkg_cmake_install()
vcpkg_copy_pdbs()
vcpkg_fixup_pkgconfig()
vcpkg_cmake_config_fixup(CONFIG_PATH lib/cmake)

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share"
                    "${CURRENT_PACKAGES_DIR}/debug/include"
)
vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE.md")
