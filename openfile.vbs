Set objShell = CreateObject("WScript.Shell")
Set objExec = objShell.Exec("powershell -command "& _
    """Add-Type -AssemblyName System.Windows.Forms; "& _
    "$dlg = New-Object System.Windows.Forms.OpenFileDialog; "& _
    "if ($dlg.ShowDialog() -eq 'OK') { $dlg.FileName }""")

Do While objExec.Status = 0
    WScript.Sleep 100
Loop

selectedFile = objExec.StdOut.ReadAll()
selectedFile = Trim(selectedFile) ' Убираем лишние пробелы и перенос строки

If selectedFile <> "" Then
    MsgBox "file: " & selectedFile
Else
    MsgBox "file no"
End If
