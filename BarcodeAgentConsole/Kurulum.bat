@echo off
title FastReport Barkod Ajanı Kurulum
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& { Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File """%~dp0Kurulum.ps1"""' -Verb RunAs }"
