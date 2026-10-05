#!/bin/bash
# Builds Nemoviz BC Finder.   bash "/d/Bandcamp Finder/gradi.sh"
#   bin\Nemoviz BC Finder.exe   the program
#   bin\bcf.exe                 the same with a console, for measuring (not shipped)
set -e
D="$(cd "$(dirname "$0")" && pwd)"
CSC="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
R="C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8"
REF=(-r:"C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Web.Extensions.dll"
     -r:"$R/PresentationFramework.dll" -r:"$R/PresentationCore.dll" -r:"$R/WindowsBase.dll" -r:"$R/System.Xaml.dll"
     -r:"$R/UIAutomationProvider.dll" -r:"$R/UIAutomationTypes.dll"
     -r:"$D/lib/Microsoft.Web.WebView2.Core.dll" -r:"$D/lib/Microsoft.Web.WebView2.Wpf.dll")
mkdir -p "$D/bin/podaci" "$D/bin/jezici" "$D/bin/Licenses"
"$CSC" -nologo -platform:x64 -optimize -target:winexe -win32icon:"$D/nemoviz.ico" -resource:"$D/nemoviz.ico",nemoviz.ico -out:"$D/bin/Nemoviz BC Finder.exe" "$D/src/"*.cs "${REF[@]}"
"$CSC" -nologo -platform:x64 -optimize -win32icon:"$D/nemoviz.ico" -resource:"$D/nemoviz.ico",nemoviz.ico -out:"$D/bin/bcf.exe" "$D/src/"*.cs "${REF[@]}"
cp "$D/lib/"*.dll "$D/bin/"
cp "$D/podaci/drzave.txt" "$D/podaci/zanrovi.txt" "$D/bin/podaci/"
cp "$D/jezici/"*.lang "$D/bin/jezici/" 2>/dev/null || true
cp "$D/Licenses/"*.txt "$D/bin/Licenses/"
rm -f "$D/bin/nemoviz.ico"
rm -f "$D/bin/BandcampFinder.exe"
echo "gotovo: $D/bin/Nemoviz BC Finder.exe"
