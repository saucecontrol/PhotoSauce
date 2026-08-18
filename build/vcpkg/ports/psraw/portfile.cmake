file(COPY
    "${CURRENT_PORT_DIR}/CMakeLists.txt"
    "${CURRENT_PORT_DIR}/psraw.cpp"
    "${CURRENT_PORT_DIR}/psraw.h"
    "${CURRENT_PORT_DIR}/psraw.ver"
    DESTINATION "${CURRENT_BUILDTREES_DIR}/src"
)

vcpkg_cmake_configure(SOURCE_PATH "${CURRENT_BUILDTREES_DIR}/src")
vcpkg_cmake_install()
vcpkg_copy_pdbs()

file(REMOVE_RECURSE
    "${CURRENT_PACKAGES_DIR}/debug/include"
    "${CURRENT_PACKAGES_DIR}/debug/share"
)

vcpkg_install_copyright(FILE_LIST "${CURRENT_PORT_DIR}/../../../../license")
