set(EXTRA_PATCHES "")
if (VCPKG_TARGET_IS_WINDOWS AND NOT VCPKG_TARGET_IS_MINGW)
    list(APPEND EXTRA_PATCHES msvc.diff)
endif()

vcpkg_download_distfile(ARCHIVE
    URLS "https://sourceforge.net/code-snapshots/git/g/gi/giflib/code.git/giflib-code-a8e3114a81f0987a61d06a41c99fd7cc2d58232c.zip"
    FILENAME "giflib-code-a8e3114a81f0987a61d06a41c99fd7cc2d58232c.zip"
    SHA512 f331d6e5e668fcb7af324fbe9f492f862266260386e9978762f77ce8f0aedd14b9215795bec127ac1c5bf6458876f2a0898d61972f879cc6eb8cddab389188ea
)

vcpkg_extract_source_archive(
    SOURCE_PATH
    ARCHIVE "${ARCHIVE}"
    PATCHES
        ${EXTRA_PATCHES}
)

file(COPY "${CMAKE_CURRENT_LIST_DIR}/CMakeLists.txt" DESTINATION "${SOURCE_PATH}")

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        "-DGIFLIB_EXPORTS=${CMAKE_CURRENT_LIST_DIR}/exports.def"
    OPTIONS_DEBUG
        -DGIFLIB_SKIP_HEADERS=ON
)

vcpkg_cmake_install()
vcpkg_copy_pdbs()

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/COPYING")
