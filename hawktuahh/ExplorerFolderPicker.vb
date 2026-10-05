Imports System.Runtime.InteropServices

''' <summary>Windows Explorer-style folder picker (the modern IFileOpenDialog in folder mode).</summary>
Public NotInheritable Class ExplorerFolderPicker
    Private Sub New()
    End Sub

    <ComImport(), Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IFileDialog
        <PreserveSig()> Function Show(hwndOwner As IntPtr) As Integer
        Sub SetFileTypes(cFileTypes As UInteger, rgFilterSpec As IntPtr)
        Sub SetFileTypeIndex(iFileType As UInteger)
        Sub GetFileTypeIndex(ByRef piFileType As UInteger)
        Sub Advise(pfde As IntPtr, ByRef pdwCookie As UInteger)
        Sub Unadvise(dwCookie As UInteger)
        Sub SetOptions(fos As UInteger)
        Sub GetOptions(ByRef pfos As UInteger)
        Sub SetDefaultFolder(psi As IShellItem)
        Sub SetFolder(psi As IShellItem)
        Sub GetFolder(ByRef ppsi As IShellItem)
        Sub GetCurrentSelection(ByRef ppsi As IShellItem)
        Sub SetFileName(<MarshalAs(UnmanagedType.LPWStr)> pszName As String)
        Sub GetFileName(ByRef pszName As IntPtr)
        Sub SetTitle(<MarshalAs(UnmanagedType.LPWStr)> pszTitle As String)
        Sub SetOkButtonLabel(<MarshalAs(UnmanagedType.LPWStr)> pszText As String)
        Sub SetFileNameLabel(<MarshalAs(UnmanagedType.LPWStr)> pszLabel As String)
        Sub GetResult(ByRef ppsi As IShellItem)
    End Interface

    <ComImport(), Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IShellItem
        Sub BindToHandler(pbc As IntPtr, ByRef bhid As Guid, ByRef riid As Guid, ByRef ppv As IntPtr)
        Sub GetParent(ByRef ppsi As IShellItem)
        Sub GetDisplayName(sigdnName As UInteger, ByRef ppszName As IntPtr)
        Sub GetAttributes(sfgaoMask As UInteger, ByRef psfgaoAttribs As UInteger)
        Sub Compare(psi As IShellItem, hint As UInteger, ByRef piOrder As Integer)
    End Interface

    Private Declare Unicode Function SHCreateItemFromParsingName Lib "shell32.dll" (pszPath As String, pbc As IntPtr, ByRef riid As Guid, ByRef ppv As IShellItem) As Integer

    Private Const FOS_PICKFOLDERS As UInteger = &H20UI
    Private Const FOS_FORCEFILESYSTEM As UInteger = &H40UI
    Private Const FOS_PATHMUSTEXIST As UInteger = &H800UI
    Private Const SIGDN_FILESYSPATH As UInteger = &H80058000UI

    ''' <summary>Returns the chosen folder, or Nothing if cancelled.</summary>
    Public Shared Function Pick(owner As IWin32Window, initialPath As String, title As String) As String
        Try
            Dim dlg As IFileDialog = DirectCast(Activator.CreateInstance(Type.GetTypeFromCLSID(New Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7"))), IFileDialog)
            Dim opts As UInteger = 0
            dlg.GetOptions(opts)
            dlg.SetOptions(opts Or FOS_PICKFOLDERS Or FOS_FORCEFILESYSTEM Or FOS_PATHMUSTEXIST)
            dlg.SetTitle(title)

            If Not String.IsNullOrWhiteSpace(initialPath) AndAlso System.IO.Directory.Exists(initialPath) Then
                Dim iid As Guid = GetType(IShellItem).GUID
                Dim item As IShellItem = Nothing
                If SHCreateItemFromParsingName(initialPath, IntPtr.Zero, iid, item) = 0 AndAlso item IsNot Nothing Then
                    dlg.SetFolder(item)
                End If
            End If

            Dim hwnd As IntPtr = If(owner IsNot Nothing, owner.Handle, IntPtr.Zero)
            If dlg.Show(hwnd) <> 0 Then Return Nothing

            Dim result As IShellItem = Nothing
            dlg.GetResult(result)
            Dim ptr As IntPtr = IntPtr.Zero
            result.GetDisplayName(SIGDN_FILESYSPATH, ptr)
            Dim path As String = Marshal.PtrToStringUni(ptr)
            Marshal.FreeCoTaskMem(ptr)
            Return path
        Catch ex As COMException
            Return PickLegacy(owner, initialPath)
        Catch ex As InvalidCastException
            Return PickLegacy(owner, initialPath)
        End Try
    End Function

    Private Shared Function PickLegacy(owner As IWin32Window, initialPath As String) As String
        Using fb As New FolderBrowserDialog()
            If System.IO.Directory.Exists(initialPath) Then fb.SelectedPath = initialPath
            Return If(fb.ShowDialog(owner) = DialogResult.OK, fb.SelectedPath, Nothing)
        End Using
    End Function
End Class
