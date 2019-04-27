#!/bin/bash

echo Start to compile protobuf ...

PROTOC_PATH=../external/protobuf/src/protoc
#PROTOC_PATH=protoc
PROTOFILE_PATH=../res/proto
PROTOSRC_BASE=../bin/ProtobufSource
PROTOSRC_SERVER=../src/ProtoRes

PROTOCPP_PATH=$PROTOSRC_BASE/cpp
PROTOCSHARP_PATH=$PROTOSRC_BASE/csharp
PROTOJAVASCRIPT_PATH=$PROTOSRC_BASE/javascript
PROTOPYTHON_PATH=$PROTOSRC_BASE/python
#PROTORUBY_PATH=$PROTOSRC_BASE/ruby
#PROTOBINARY_PATH=$PROTOSRC_BASE/pb

if [ ! -d ../bin ]; then
    mkdir ../bin
fi
if [ ! -d $PROTOSRC_BASE ]; then
    mkdir $PROTOSRC_BASE
fi
if [ ! -d $PROTOCPP_PATH ]; then
    mkdir $PROTOCPP_PATH
fi
if [ ! -d $PROTOCSHARP_PATH ]; then
    mkdir $PROTOCSHARP_PATH
fi
if [ ! -d $PROTOJAVASCRIPT_PATH ]; then
    mkdir $PROTOJAVASCRIPT_PATH
fi
if [ ! -d $PROTOPYTHON_PATH ]; then
    mkdir $PROTOPYTHON_PATH
fi
#if [ ! -d $PROTORUBY_PATH ]; then
#    mkdir $PROTORUBY_PATH
#fi
#if [ ! -d $PROTOBINARY_PATH ]; then
#    mkdir $PROTOBINARY_PATH
#fi

${PROTOC_PATH} -I=$PROTOFILE_PATH --cpp_out=$PROTOCPP_PATH --csharp_out=$PROTOCSHARP_PATH --js_out=$PROTOJAVASCRIPT_PATH --python_out=$PROTOPYTHON_PATH $PROTOFILE_PATH/ArmyAntMessage/Common/base.proto
${PROTOC_PATH} -I=$PROTOFILE_PATH --cpp_out=$PROTOCPP_PATH --csharp_out=$PROTOCSHARP_PATH --js_out=$PROTOJAVASCRIPT_PATH --python_out=$PROTOPYTHON_PATH $PROTOFILE_PATH/ArmyAntMessage/System/SocketHead.proto
${PROTOC_PATH} -I=$PROTOFILE_PATH --cpp_out=$PROTOCPP_PATH --csharp_out=$PROTOCSHARP_PATH --js_out=$PROTOJAVASCRIPT_PATH --python_out=$PROTOPYTHON_PATH $PROTOFILE_PATH/ArmyAntMessage/System/SessionStart.proto
${PROTOC_PATH} -I=$PROTOFILE_PATH --cpp_out=$PROTOCPP_PATH --csharp_out=$PROTOCSHARP_PATH --js_out=$PROTOJAVASCRIPT_PATH --python_out=$PROTOPYTHON_PATH $PROTOFILE_PATH/ArmyAntMessage/DBProxy/SqlRequest.proto

${PROTOC_PATH} -I=$PROTOFILE_PATH --cpp_out=$PROTOCPP_PATH --csharp_out=$PROTOCSHARP_PATH --js_out=$PROTOJAVASCRIPT_PATH --python_out=$PROTOPYTHON_PATH $PROTOFILE_PATH/ArmyAntMessage/SubApps/SimpleEcho.proto
${PROTOC_PATH} -I=$PROTOFILE_PATH --cpp_out=$PROTOCPP_PATH --csharp_out=$PROTOCSHARP_PATH --js_out=$PROTOJAVASCRIPT_PATH --python_out=$PROTOPYTHON_PATH $PROTOFILE_PATH/ArmyAntMessage/SubApps/Huolong.proto

# TODO: copy csharp source code files to PROTOSRC_SERVER

# read -rsp $'Finished !\nPress any key to exit.\n' -n 1 key
# echo $key
