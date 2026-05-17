#InputLevel 3
#MaxThreadsPerHotkey 1
#InstallMouseHook
; Contributed by Marctraider
Process, Priority,, High
SetBatchLines, -1

SendMode Input
SetMouseDelay, -1

SLEEP_DELAY := 17
SLEEP_DELAYUP := 10

leftdown := 0
clickX := 0
clickY := 0

LButton::
    Sleep, %SLEEP_DELAY%

    MouseGetPos, clickX, clickY

    Click, down, left
    leftdown := 1

    SetTimer, ForceLeftUp, -200
return

LButton Up::
    if (leftdown)
    {
        Sleep, %SLEEP_DELAYUP%
        Click, up, left
        leftdown := 0
    }
return

ForceLeftUp:
    SetTimer, ForceLeftUp, Off

    if (leftdown)
    {
        MouseGetPos, curX, curY

        ; only release if mouse has NOT moved (no drag happening)
        if (curX = clickX && curY = clickY)
        {
            Click, up, left
            leftdown := 0
        }
    }
return

RButton::
    Sleep, %SLEEP_DELAY%
    Click, down, right
    KeyWait, RButton
    Click, up, right
return
