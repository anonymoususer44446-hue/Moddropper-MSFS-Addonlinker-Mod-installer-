Imports System.Drawing.Drawing2D

Public Module Theme
    Public ReadOnly BgColor As Color = Color.FromArgb(18, 18, 22)
    Public ReadOnly PanelColor As Color = Color.FromArgb(28, 28, 34)
    Public ReadOnly SurfaceColor As Color = Color.FromArgb(40, 40, 48)
    Public ReadOnly SurfaceHotColor As Color = Color.FromArgb(54, 54, 64)
    Public ReadOnly BorderColor As Color = Color.FromArgb(62, 62, 74)
    Public ReadOnly TextColor As Color = Color.FromArgb(236, 236, 242)
    Public ReadOnly MutedColor As Color = Color.FromArgb(145, 145, 160)
    Public ReadOnly AccentColor As Color = Color.FromArgb(88, 140, 255)
    Public ReadOnly AccentEndColor As Color = Color.FromArgb(150, 110, 255)

    Private Declare Function DwmSetWindowAttribute Lib "dwmapi.dll" (hwnd As IntPtr, attr As Integer, ByRef value As Integer, size As Integer) As Integer

    Public Sub Apply(form As Form)
        Try
            form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try
        form.BackColor = BgColor
        form.ForeColor = TextColor
        form.Font = New Font("Segoe UI", 9.5F)
        AddHandler form.HandleCreated, Sub() DarkTitleBar(form)
        If form.IsHandleCreated Then DarkTitleBar(form)
        StyleChildren(form)
    End Sub

    Private Sub DarkTitleBar(form As Form)
        Try
            Dim on1 As Integer = 1
            If DwmSetWindowAttribute(form.Handle, 20, on1, 4) <> 0 Then DwmSetWindowAttribute(form.Handle, 19, on1, 4)
        Catch
        End Try
    End Sub

    Private Sub StyleChildren(parent As Control)
        For Each c As Control In parent.Controls
            If TypeOf c Is Button Then
                Dim b As Button = DirectCast(c, Button)
                b.FlatStyle = FlatStyle.Flat
                b.FlatAppearance.BorderColor = BorderColor
                b.FlatAppearance.MouseOverBackColor = SurfaceHotColor
                b.FlatAppearance.MouseDownBackColor = AccentColor
                b.BackColor = SurfaceColor
                b.ForeColor = TextColor
                b.Cursor = Cursors.Hand
                b.UseVisualStyleBackColor = False
            ElseIf TypeOf c Is TextBox Then
                c.BackColor = SurfaceColor
                c.ForeColor = TextColor
                DirectCast(c, TextBox).BorderStyle = BorderStyle.FixedSingle
            ElseIf TypeOf c Is ComboBox Then
                StyleCombo(DirectCast(c, ComboBox))
            ElseIf TypeOf c Is ListBox Then
                Dim l As ListBox = DirectCast(c, ListBox)
                l.BorderStyle = BorderStyle.None
                l.BackColor = PanelColor
                l.ForeColor = TextColor
            ElseIf TypeOf c Is CheckBox Then
                c.ForeColor = TextColor
            ElseIf TypeOf c Is Label Then
                If Not Equals(c.Tag, "keep") Then c.ForeColor = TextColor
            End If
            If c.HasChildren Then StyleChildren(c)
        Next
    End Sub

    Private Sub StyleCombo(cb As ComboBox)
        cb.FlatStyle = FlatStyle.Flat
        cb.BackColor = SurfaceColor
        cb.ForeColor = TextColor
        cb.DrawMode = DrawMode.OwnerDrawFixed
        cb.ItemHeight = 22
        AddHandler cb.DrawItem, Sub(s, e)
                                    If e.Index < 0 Then Return
                                    Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
                                    Using bg As New SolidBrush(If(selected, AccentColor, SurfaceColor))
                                        e.Graphics.FillRectangle(bg, e.Bounds)
                                    End Using
                                    Using fg As New SolidBrush(TextColor)
                                        e.Graphics.DrawString(cb.Items(e.Index).ToString(), cb.Font, fg, e.Bounds.X + 4, e.Bounds.Y + 2)
                                    End Using
                                End Sub
    End Sub

    Public Function RoundRect(r As Rectangle, radius As Integer) As GraphicsPath
        Dim d As Integer = radius * 2
        Dim p As New GraphicsPath()
        p.AddArc(r.X, r.Y, d, d, 180, 90)
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        p.CloseFigure()
        Return p
    End Function
End Module

Public Class DarkProgressBar
    Inherits Control

    Private _value As Integer

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
        Height = 20
    End Sub

    Public Property Value As Integer
        Get
            Return _value
        End Get
        Set(v As Integer)
            v = Math.Max(0, Math.Min(100, v))
            If v = _value Then Return
            _value = v
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.Clear(Parent.BackColor)
        Dim r As New Rectangle(0, 0, Width - 1, Height - 1)
        If r.Width < 4 OrElse r.Height < 4 Then Return
        Dim radius As Integer = r.Height \ 2

        Using path As GraphicsPath = Theme.RoundRect(r, radius), track As New SolidBrush(Theme.SurfaceColor)
            g.FillPath(track, path)
        End Using

        Dim fillW As Integer = CInt(r.Width * _value / 100.0)
        If fillW >= r.Height Then
            Dim fr As New Rectangle(r.X, r.Y, fillW, r.Height)
            Using path As GraphicsPath = Theme.RoundRect(fr, radius), br As New LinearGradientBrush(fr, Theme.AccentColor, Theme.AccentEndColor, LinearGradientMode.Horizontal)
                g.FillPath(br, path)
            End Using
        End If

        Using sf As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}, fg As New SolidBrush(Theme.TextColor)
            g.DrawString(_value & "%", Font, fg, New RectangleF(0, 0, Width, Height), sf)
        End Using
    End Sub
End Class
