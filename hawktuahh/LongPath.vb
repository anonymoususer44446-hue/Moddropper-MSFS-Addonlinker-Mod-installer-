Imports System.ComponentModel
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports Microsoft.Win32.SafeHandles

Public Module LongPath

    Public Function ToLong(p As String) As String
        If String.IsNullOrEmpty(p) Then Return p
        Dim full As String = Path.GetFullPath(p)
        If full.StartsWith("\\?\", StringComparison.Ordinal) Then Return full
        If full.StartsWith("\\", StringComparison.Ordinal) Then Return "\\?\UNC\" & full.Substring(2)
        Return "\\?\" & full
    End Function

    Public Function LeafName(p As String) As String
        Return Path.GetFileName(p.TrimEnd("\"c, "/"c))
    End Function

    Public Function AnyExists(p As String) As Boolean
        Try
            File.GetAttributes(ToLong(p))
            Return True
        Catch ex As FileNotFoundException
            Return False
        Catch ex As DirectoryNotFoundException
            Return False
        End Try
    End Function

    Public Function IsLink(p As String) As Boolean
        Try
            Return (File.GetAttributes(ToLong(p)) And FileAttributes.ReparsePoint) = FileAttributes.ReparsePoint
        Catch ex As IOException
            Return False
        End Try
    End Function

    Public Sub DeleteAny(p As String)
        Dim lp As String = ToLong(p)
        Dim a As FileAttributes = File.GetAttributes(lp)
        Dim isDir As Boolean = (a And FileAttributes.Directory) = FileAttributes.Directory
        Dim isLnk As Boolean = (a And FileAttributes.ReparsePoint) = FileAttributes.ReparsePoint
        If isDir Then
            Directory.Delete(lp, Not isLnk)
        Else
            File.SetAttributes(lp, FileAttributes.Normal)
            File.Delete(lp)
        End If
    End Sub

    Private Function SameRoot(a As String, b As String) As Boolean
        Return String.Equals(Path.GetPathRoot(Path.GetFullPath(a)),
                             Path.GetPathRoot(Path.GetFullPath(b)),
                             StringComparison.OrdinalIgnoreCase)
    End Function

    Public Sub EnsureParent(p As String)
        Dim parent As String = Path.GetDirectoryName(Path.GetFullPath(p))
        If parent IsNot Nothing Then Directory.CreateDirectory(ToLong(parent))
    End Sub

    Public Sub MoveDir(src As String, dest As String)
        EnsureParent(dest)
        If SameRoot(src, dest) Then
            Directory.Move(ToLong(src), ToLong(dest))
        Else
            CopyDirInternal(ToLong(src), ToLong(dest))
            DeleteAny(src)
        End If
    End Sub

    Public Sub MoveFile(src As String, dest As String)
        EnsureParent(dest)
        If File.Exists(ToLong(dest)) Then File.Delete(ToLong(dest))
        File.Move(ToLong(src), ToLong(dest))
    End Sub

    Private Sub CopyDirInternal(src As String, dest As String)
        Directory.CreateDirectory(dest)
        For Each f As String In Directory.GetFiles(src)
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), True)
        Next
        For Each d As String In Directory.GetDirectories(src)
            CopyDirInternal(d, Path.Combine(dest, Path.GetFileName(d)))
        Next
    End Sub

End Module

Public NotInheritable Class LinkHelper

    Private Sub New()
    End Sub

    Private Const SYMBOLIC_LINK_FLAG_DIRECTORY As UInteger = &H1UI
    Private Const SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE As UInteger = &H2UI
    Private Const GENERIC_WRITE As UInteger = &H40000000UI
    Private Const OPEN_EXISTING As UInteger = 3UI
    Private Const FILE_FLAG_BACKUP_SEMANTICS As UInteger = &H2000000UI
    Private Const FILE_FLAG_OPEN_REPARSE_POINT As UInteger = &H200000UI
    Private Const FSCTL_SET_REPARSE_POINT As UInteger = &H900A4UI
    Private Const IO_REPARSE_TAG_MOUNT_POINT As UInteger = &HA0000003UI

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function CreateSymbolicLinkW(lpSymlinkFileName As String, lpTargetFileName As String, dwFlags As UInteger) As <MarshalAs(UnmanagedType.I1)> Boolean
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function CreateFileW(lpFileName As String, dwDesiredAccess As UInteger, dwShareMode As UInteger, lpSecurityAttributes As IntPtr, dwCreationDisposition As UInteger, dwFlagsAndAttributes As UInteger, hTemplateFile As IntPtr) As SafeFileHandle
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function DeviceIoControl(hDevice As SafeFileHandle, dwIoControlCode As UInteger, lpInBuffer As Byte(), nInBufferSize As UInteger, lpOutBuffer As IntPtr, nOutBufferSize As UInteger, ByRef lpBytesReturned As UInteger, lpOverlapped As IntPtr) As Boolean
    End Function

    Public Shared Function CreateDirectoryLink(linkPath As String, targetDir As String) As String
        Dim target As String = Path.GetFullPath(targetDir).TrimEnd("\"c)
        Dim linkLong As String = LongPath.ToLong(linkPath)
        LongPath.EnsureParent(linkPath)

        If CreateSymbolicLinkW(linkLong, target, SYMBOLIC_LINK_FLAG_DIRECTORY Or SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE) Then
            Return "symbolic link"
        End If
        Dim symErr As Integer = Marshal.GetLastWin32Error()

        Try
            CreateJunction(linkLong, target)
            Return "junction"
        Catch ex As Exception
            Throw New IOException($"Could not create a link (symlink error {symErr}; junction error: {ex.Message}). " &
                                  "Turn on Windows Developer Mode or run the app as administrator.")
        End Try
    End Function

    Private Shared Sub CreateJunction(linkLong As String, target As String)
        Directory.CreateDirectory(linkLong)
        Try
            Dim subBytes As Byte() = Encoding.Unicode.GetBytes("\??\" & target)
            Dim printBytes As Byte() = Encoding.Unicode.GetBytes(target)
            Dim dataLen As Integer = 8 + subBytes.Length + 2 + printBytes.Length + 2
            Dim buf(8 + dataLen - 1) As Byte

            BitConverter.GetBytes(IO_REPARSE_TAG_MOUNT_POINT).CopyTo(buf, 0)
            BitConverter.GetBytes(CUShort(dataLen)).CopyTo(buf, 4)
            BitConverter.GetBytes(CUShort(0)).CopyTo(buf, 8)
            BitConverter.GetBytes(CUShort(subBytes.Length)).CopyTo(buf, 10)
            BitConverter.GetBytes(CUShort(subBytes.Length + 2)).CopyTo(buf, 12)
            BitConverter.GetBytes(CUShort(printBytes.Length)).CopyTo(buf, 14)
            subBytes.CopyTo(buf, 16)
            printBytes.CopyTo(buf, 16 + subBytes.Length + 2)

            Using h As SafeFileHandle = CreateFileW(linkLong, GENERIC_WRITE, 0UI, IntPtr.Zero, OPEN_EXISTING,
                                                    FILE_FLAG_BACKUP_SEMANTICS Or FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero)
                If h.IsInvalid Then Throw New Win32Exception(Marshal.GetLastWin32Error())
                Dim returned As UInteger = 0UI
                If Not DeviceIoControl(h, FSCTL_SET_REPARSE_POINT, buf, CUInt(buf.Length), IntPtr.Zero, 0UI, returned, IntPtr.Zero) Then
                    Throw New Win32Exception(Marshal.GetLastWin32Error())
                End If
            End Using
        Catch
            Try
                Directory.Delete(linkLong, False)
            Catch
            End Try
            Throw
        End Try
    End Sub

End Class
