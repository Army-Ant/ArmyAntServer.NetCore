
:: Copyright (c) 2015 ArmyAnt
:: ��Ȩ���� (c) 2015 ArmyAnt
::
:: Licensed under the BSD License, Version 2.0 (the License);
:: �����ʹ��BSDЭ�鱣��, Э��汾:2.0
:: you may not use this file except in compliance with the License.
:: ʹ�ñ���Դ�����ļ�������, ��Ϊͬ��Э��
:: You can read the license content in the file "LICENSE" at the root of this project
:: �������ڱ���Ŀ�ĸ�Ŀ¼�ҵ���Ϊ"LICENSE"���ļ�, ���Ķ�Э������
:: You may also obtain a copy of the License at
:: ��Ҳ�����ڴ˴����Э��ĸ���:
::
::     http://opensource.org/licenses/BSD-3-Clause
::
:: Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an AS IS BASIS,
:: WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
:: ���Ƿ���Ҫ����߰�Ȩ����������ͬ��,������ڱ�Э������ϵķ���û���κ���ʽ�������͵���,������ʾ�Ļ�Ĭ���.
:: See the License for the specific language governing permissions and limitations under the License.
:: �����ض����ƻ����Թ���Ȩ�����Ķ�Э��
:: This file is the internal source file of this project, is not contained by the closed source release part of this software
:: ���ļ�Ϊ�ڲ�Դ���ļ�, ��������ڱ�Դ�����ı������

:: Created by Jason 4/26/2019
:: This is a batch command script file used to do something before build in Visual Studio project
:: Please edit it in notepad++ or another text editor, instead of visual studio editor

set dir=%1
cd %dir%
compile_protobuf.bat
