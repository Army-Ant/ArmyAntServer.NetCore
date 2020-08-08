git submodule update --init --progress --recursive
cd ArmyAnt.js
git checkout master
cd ../ArmyAnt.Net
git checkout master
cd ../MySqlConnector
git checkout master
cd ../../res
git checkout master
cd ../external/protobuf
git checkout master
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
pause