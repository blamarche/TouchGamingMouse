#InputLevel 3
;SendMode Input
SLEEP_DELAY:=2 ;this will need to be adjusted per machine performance most likely
               ;as it works around the weird microsoft behavior in directx games

SLEEP_DELAYUP:=50

SetDefaultMouseSpeed, 0
SetMouseDelay, -1
everyother:=0
everyother_d:=0
middledown:=0
uptoggle:=0
hoverToggle:=0
lasttime:=0
lasttime_d:=0

Shift & LButton::
  Click, down, left
  KeyWait, LButton
  Click, up, left
  return
  
Control & LButton::
  Click, down, left
  KeyWait, LButton
  Click, up, left
  return
  
Alt & LButton::
  Click, down, left
  KeyWait, LButton
  Click, up, left
  return
  
Shift & RButton::
  Click, down, right
  KeyWait, RButton
  Click, up, right
  return
  
Control & RButton::
  Click, down, right
  KeyWait, RButton
  Click, up, right
  return
  
Alt & RButton::
  Click, down, right
  KeyWait, RButton
  Click, up, right
  return

leftdn:=0
LButton Up::
  if (middledown=1)
  {
	Sleep, SLEEP_DELAYUP
	Click, up, Middle
  }
  if (rightdown=1)
  {
	Sleep, SLEEP_DELAYUP  
	Click, up, right
  }
  if (leftdn=1)
  {
        Sleep, SLEEP_DELAYUP
        Click, up, left
        leftdn:=0
  }
Return

LButton::
  if (uptoggle=1)
  {
    if (middledown=1)
    {
      Click, up, Middle
      Sleep, 5
    }
    Click, down, Middle
    SoundBeep
    middledown:=1    
  }
  else if (ruptoggle) 
  {
	if (rightdown=1)
    {
      Click, up, Right
      Sleep, 5
    }
    Click, down, Right
    SoundBeep, 300
    rightdown:=1    
  }
  else if (hoverToggle=1)
  {
    ;do nothing
  }
  else 
  {
    Sleep, SLEEP_DELAY
    Click, down, left
    leftdn:=1
  }
  Return

RButton::
  Sleep, SLEEP_DELAY
  Click, down, right
  KeyWait, % A_ThisHotkey
  Click, up, right
  Return

;F13, F14 and F15 are special cases for TouchGamingMouse overlay
F13::
  hoverToggle:=!hoverToggle
  if (hoverToggle=1)
  {            
    SoundBeep, 350
  }  
  return

F14::
  if (middledown=1)
  {
    middledown:=0
    Click, up, Middle
  }
  uptoggle:=!uptoggle
  if (uptoggle=1)
  {            
    ;SoundBeep
  }  
  return
  
F15::
  if (rightdown=1)
  {
    rightdown:=0
    Click, up, Right
  }
  ruptoggle:=!ruptoggle
  if (ruptoggle=1)
  {            
    ;SoundBeep, 300
  }  
  return


;ScrollLock::
  ;TODO: Lmouse toggle
  ;SoundBeep, 2000
  ;Return

Volume_Down::
  ;everyother_d:=!everyother_d
  ;if (everyother_d=1)
  ;{
    send {WheelDown 1}
  ;}
  Return

Volume_Up::  
  ;everyother:=!everyother
  ;if (everyother=1)
  ;{
    send {WheelUp 1}
  ;}
  Return