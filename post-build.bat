@echo off

rem Clear build tools so they don't get detected and published by MyGet
if exist buildpackages (
    rmdir /S /Q buildpackages
)
