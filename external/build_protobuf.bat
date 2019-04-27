cd protobuf
git checkout master
git submodule update --init --progress --recursive
cd cmake
mkdir build_win
cd build_win
mkdir debug
mkdir release
cd debug
cmake -G "NMake Makefiles" -DCMAKE_BUILD_TYPE=Debug -DCMAKE_INSTALL_PREFIX=../../.. ../..
nmake
nmake install
cd ../release
cmake -G "NMake Makefiles" -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=../../.. ../..
nmake
nmake install
