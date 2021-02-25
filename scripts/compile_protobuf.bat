@echo on
cd ..

:set_path
set PROTOC_PATH=external\protobuf\bin
set PROTOFILE_PATH=res\proto
set PROTOSRC_BASE=bin\ProtobufSource
set PROTOSRC_SERVER=src\ProtoRes\res

set PROTOCPP_PATH=%PROTOSRC_BASE%\cpp
set PROTOCSHARP_PATH=%PROTOSRC_BASE%\csharp
set PROTOJAVASCRIPT_PATH=%PROTOSRC_BASE%\javascript
set PROTOPYTHON_PATH=%PROTOSRC_BASE%\python
::not used
set PROTORUBY_PATH=%PROTOSRC_BASE%\ruby
::not used
set PROTOBINARY_PATH=%PROTOSRC_BASE%\pb

set EXECUTE_BINARY=bin
set PROTOCOMMONJS_PATH=%EXECUTE_BINARY%\proto-js

:set_source_file_list
::base
set PROTOFILES=%PROTOFILE_PATH%\ArmyAntMessage\Common\base.proto
set PROTOFILES=%PROTOFILES% %PROTOFILE_PATH%\ArmyAntMessage\System\SocketHead.proto
set PROTOFILES=%PROTOFILES% %PROTOFILE_PATH%\ArmyAntMessage\System\SessionStart.proto
::sub application
set PROTOFILES=%PROTOFILES% %PROTOFILE_PATH%\ArmyAntMessage\SubApps\Gate.proto
set PROTOFILES=%PROTOFILES% %PROTOFILE_PATH%\ArmyAntMessage\SubApps\Chat.proto
set PROTOFILES=%PROTOFILES% %PROTOFILE_PATH%\ArmyAntMessage\SubApps\Huolong.proto
::database proxy
set PROTODBPROXYFILES=%PROTOFILE_PATH%\ArmyAntMessage\DBProxy\SqlRequest.proto

:create_noexist_path
if not exist %PROTOCPP_PATH% (mkdir %PROTOCPP_PATH%)
if not exist %PROTOCSHARP_PATH% (mkdir %PROTOCSHARP_PATH%)
if not exist %PROTOJAVASCRIPT_PATH% (mkdir %PROTOJAVASCRIPT_PATH%)
if not exist %PROTOPYTHON_PATH% (mkdir %PROTOPYTHON_PATH%)
if not exist %PROTORUBY_PATH% (mkdir %PROTORUBY_PATH%)
if not exist %PROTOBINARY_PATH% (mkdir %PROTOBINARY_PATH%)
if not exist %EXECUTE_BINARY% (mkdir %EXECUTE_BINARY%)
if not exist %PROTOCOMMONJS_PATH% (mkdir %PROTOCOMMONJS_PATH%)
if not exist %PROTOSRC_SERVER% (mkdir %PROTOSRC_SERVER%)

:execute_protoc
%PROTOC_PATH%\protoc.exe -I=%PROTOFILE_PATH% --cpp_out=%PROTOCPP_PATH% --csharp_out=%PROTOCSHARP_PATH% --js_out=library=aaserver_proto,binary:%PROTOJAVASCRIPT_PATH% --python_out=%PROTOPYTHON_PATH% %PROTOFILES% %PROTODBPROXYFILES%
%PROTOC_PATH%\protoc.exe -I=%PROTOFILE_PATH% --js_out=import_style=commonjs,binary:%PROTOCOMMONJS_PATH% %PROTOFILES%

:copy_js_used_file
copy res\proto\proto_message_code_helper.js %PROTOCOMMONJS_PATH%
xcopy %PROTOCSHARP_PATH% %PROTOSRC_SERVER% /E /C /Q /Y /I

cd scripts
:End
