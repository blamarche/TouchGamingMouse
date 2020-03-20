rollCmd:="/r "
cmdOn:=0

Return::
  Send {Return down}
  KeyWait, % A_ThisHotkey
  Send {Return up}
  cmdOn:=0
  return

;modifiers
LCtrl & F1::
  Send {Text}-2
  Send {Return}
  cmdOn:=0
  return
LCtrl & F2::
  Send {Text}-1
  Send {Return}
  cmdOn:=0
  return
LCtrl & F3::
  Send {Text}+1
  Send {Return}
  cmdOn:=0
  return
LCtrl & F4::
  Send {Text}+2
  Send {Return}
  cmdOn:=0
  return
LCtrl & F5::
  Send {Text}+3
  Send {Return}
  cmdOn:=0
  return
LCtrl & F6::
  Send {Text}+4
  Send {Return}
  cmdOn:=0
  return
LCtrl & F7::
  Send {Text}+5
  Send {Return}
  cmdOn:=0
  return
LCtrl & F8::
  Send {Text}+6
  Send {Return}
  cmdOn:=0
  return
LCtrl & F10::
  Send {Text}+7
  Send {Return}
  cmdOn:=0
  return
LCtrl & F9::
  Send {Return}
  cmdOn:=0
  return
  
;dice amount and cmd
F8::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 1  
  cmdOn:=1
  return
F9::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 2
  cmdOn:=1
  return
F10::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 3
  cmdOn:=1
  return
F11::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 4
  cmdOn:=1
  return
F12::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 5
  cmdOn:=1
  return
F13::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 6
  cmdOn:=1
  return
F14::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 7
  cmdOn:=1
  return
F15::
  if (cmdOn=0) {
   Send %rollCmd%
  }
  Send 8
  cmdOn:=1
  return

;basic dice
F1::
  if (cmdOn=0) {
   Send %rollCmd%
   cmdOn:=1
  }
  Send d4
  return  
F2::
  if (cmdOn=0) {
   Send %rollCmd%
   cmdOn:=1
  }
  Send d6
  return  
F3::
if (cmdOn=0) {
   Send %rollCmd%
   cmdOn:=1
  }
  Send d8
  return  
F4::
  if (cmdOn=0) {
   Send %rollCmd%
   cmdOn:=1
  }
  Send d10
  return  
F5::
  if (cmdOn=0) {
   Send %rollCmd%
   cmdOn:=1
  }
  Send d12
  return  
F6::
  if (cmdOn=0) {
   Send %rollCmd%
   cmdOn:=1
  }
  Send d20
  return
F7::
  if (cmdOn=0) {
   Send %rollCmd%
   cmdOn:=1
  }
  Send d100
  return
