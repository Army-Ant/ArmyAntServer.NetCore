#!/bin/bash

#Compile
echo "Compile protobuf"
cd ../scripts
bash compile_protobuf.sh
cd ../test

#Copy_Javascript
echo "Copy protobuf javascript files to test directory"
if [ -d proto-js ]; then
    rm -rf proto-js
fi
mkdir proto-js
cp -r ../src/ProtobufSource/javascript/* ./proto-js
if [ ! -d libprotobuf-js ]; then
    cp -r ../external/Protobuf/js ./libprotobuf-js
fi
if [ ! -d libclosure-js ]; then
    cp -r ../external/closure-library ./libclosure-js
fi
# python libclosure-js/closure/bin/calcdeps.py -i libprotobuf-js/ -i proto-js/ -p libclosure-js/ -o script > armyantmessage.js :: This sentence cannot work well, I will use ArmyAnt.js instead
if [ ! -d ArmyAnt.js ]; then
    cp -r ../external/ArmyAnt.js ./ArmyAnt.js
    cd ArmyAnt.js
    git checkout master
    cd ..
fi

