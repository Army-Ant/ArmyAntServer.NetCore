:Compile
@echo off
echo Compile protobuf
cd ..\scripts
call compile_protobuf.bat
cd ..\test

:Copy_Javascript
echo Copy protobuf javascript files to test directory
if exist proto-js (
	rd proto-js /S /Q
)
mkdir proto-js
xcopy ..\src\ProtobufSource\javascript .\proto-js /E /C /Q /Y /I
if not exist libprotobuf-js (
	xcopy ..\external\Protobuf\js .\libprotobuf-js /E /C /Q /Y /I
)
if not exist libclosure-js (
	xcopy ..\external\closure-library .\libclosure-js /E /C /Q /Y /I
)
:: call python libclosure-js\closure\bin\calcdeps.py -i libprotobuf-js/ -i proto-js/ -p libclosure-js/ -o script > armyantmessage.js :: This sentence cannot work well, I will use ArmyAnt.js instead
if not exist ArmyAnt.js (
    xcopy ..\external\ArmyAnt.js .\ArmyAnt.js  /E /C /Q /Y /I
    cd ArmyAnt.js
    git checkout master
    cd ..
)

:Copy_CSharp
echo Copy protobuf csharp files to test directory
if exist ArmyAntServer_TestClient_CSharp\ArmyAntServer_TestClient_CSharp\protobufSource (
	rd .\ArmyAntServer_TestClient_CSharp\ArmyAntServer_TestClient_CSharp\protobufSource /S /Q
)
xcopy ..\bin\ProtobufSource\csharp .\ArmyAntServer_TestClient_CSharp\ArmyAntServer_TestClient_CSharp\protobufSource /E /C /Q /Y /I

:End
pause
