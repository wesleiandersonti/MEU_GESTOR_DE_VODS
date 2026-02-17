@echo off

rem %1 original Playlist
rem %2 masked Playlist
rem %3 new Playlist
rem %4 masked Address
rem %5 new IP-Address
rem %6 new Port
rem %7 new Address
rem %8 Country
rem %9 Network-Name


set outputfile="%~dp0clip.txt"

echo [hide] >> %outputfile%

set country=%~8
set network=%~9

echo %country% - %network% > %outputfile%

set test=[img]http://monitor.zone-game.info/check.php?do=status
set test2=ip=%~5
set test3=port=%~6
set test4=id=8[/img]
echo %test%^&%test2%^&%test3%^&%test4%>> %outputfile%

echo [spoiler] >> %outputfile%

type %3 >> %outputfile%

echo. >> %outputfile%
echo [/spoiler] >> %outputfile%
echo [/hide] >> %outputfile%

"%~dp0tools\clipboard.exe" %outputfile%
