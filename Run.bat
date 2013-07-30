@echo off
set SOURCE="D:\Projects\EngineerFirst\Output"
set DESTINATION="D:\Projects\EngineerFirst\Game\GameData"
echo SOURCE = %SOURCE%
echo DESTINATION = %DESTINATION%
echo.
xcopy %SOURCE% %DESTINATION% /D /E /C /R /I /K /Y
echo.
echo Starting KSP Build Edition...
call D:\Projects\EngineerFirst\Game\KSP.exe