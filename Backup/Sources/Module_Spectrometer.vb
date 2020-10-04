
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices

Module Spectrometer

    Private MeterBar1 As MeterBar = New MeterBar(Form_Main.PictureBox3, 4)

    'Friend Sub InitPictureboxImage(ByVal pbox As PictureBox)
    '    With pbox
    '        If .ClientSize.Height < 1 Then Return
    '        .Image = New Bitmap(.ClientSize.Width, .ClientSize.Height)
    '    End With
    'End Sub

    Friend Sub InitPictureboxImage(ByVal pbox As PictureBox)
        With pbox
            If .ClientSize.Height < 1 Then Return
            If .Image Is Nothing OrElse .Image.Width <> .ClientRectangle.Width _
                                 OrElse .Image.Height <> .ClientRectangle.Height Then
                .Image = New Bitmap(.ClientRectangle.Width, _
                                    .ClientRectangle.Height, _
                                    Imaging.PixelFormat.Format24bppRgb)
            End If
        End With
    End Sub

    Friend TrimPoint1 As Single = 436
    Friend TrimPoint2 As Single = 546

    Friend ReferenceScale As Boolean
    Friend CursorInside As Boolean
    Friend MaxNanometers_OldValue As Int32

    Private ShowDips As Boolean
    Private ShowPeaks As Boolean
    Private UseColors As Boolean
    Private TrimScale As Boolean

    Private SpecArray(-1) As Single
    Private SpecArrayFiltered(-1) As Single
    Private MaxValue As Single
    Private MaxValueX As Int32

    Private Filter As Int32
    Private Speed As Int32
    Private Flip As Boolean

    Private KFilter As Single
    Private KSpeed As Single
    Private Kred As Single
    Private Kgreen As Single
    Private Kblue As Single

    Private SrcY0 As Int32
    Private SrcDY As Int32
    Private SrcX0 As Int32
    Private SrcDX As Int32

    Private SrcImage As Image
    Private SrcBmp As Bitmap
    Private SrcW As Int32
    Private SrcH As Int32
    Private SrcPen As Pen

    Private DestPbox As PictureBox = Form_Main.PBox_Spectrum
    Private DestW As Int32
    Private DestH As Int32
    Private DestRight As Int32
    Private DestBottom As Int32
    Private ScalePen1 As Pen = New Pen(Color.FromArgb(140, 140, 140))
    Private ScalePen2 As Pen = New Pen(Color.FromArgb(200, 200, 200))
    Private ScalePen3 As Pen = New Pen(Color.FromArgb(220, 220, 220))
    Private ScaleFont As Font = New Font("Arial", 8)

    Friend NanometersMin As Single = 270
    Friend NanometersMax As Single = 1200
    Private NanometersDelta As Single

    Private NmStart As Single
    Private NmEnd As Single
    Private NmStartDiv As Int32
    Private NmCoeff As Single

    Private gfx As Graphics
    Private kx As Single
    Private ky As Single
    Private Pen_Graph As Pen = New Pen(Color.Black)


    Friend Sub Spectrometer_SetSourceParams()
        If SrcImage Is Nothing Then Return

        ' ---------------------------------------------------------------------
        SrcW = SrcImage.Width
        SrcH = SrcImage.Height
        ' ---------------------------------------------------------------------
        Flip = Form_Main.chk_Flip.Checked
        Dim StartY As Int32 = Form_Main.txt_StartY.NumericValueInteger
        Dim SizeY As Int32 = Form_Main.txt_SizeY.NumericValueInteger
        Dim StartX As Int32 = Form_Main.txt_StartX.NumericValueInteger
        Dim EndX As Int32 = Form_Main.txt_EndX.NumericValueInteger
        ' ---------------------------------------------------------------------
        SrcX0 = (SrcW * StartX) \ 1000
        SrcDX = SrcW - SrcX0 + (SrcW * (EndX - 1000)) \ 1000
        SrcDY = (SrcH * SizeY) \ 100
        SrcY0 = SrcH - (SrcH * StartY \ 100) - SrcDY
        ' ---------------------------------------------------------------------
        If SrcX0 + SrcDX > SrcImage.Width Then SrcDX = SrcW - SrcX0
        If SrcDX <= 0 Then SrcX0 += (SrcDX - 1) : SrcDX = 1
        If SrcX0 < 0 Then SrcX0 = 0
        If SrcY0 + SrcDY > SrcH Then SrcDY = SrcH - SrcY0
        If SrcDY <= 0 Then SrcY0 += (SrcDY - 1) : SrcDY = 1
        If SrcY0 < 0 Then SrcY0 = 0
        ' ---------------------------------------------------------------------
        ReDim SpecArray(SrcDX - 1)
        ReDim SpecArrayFiltered(SrcDX - 1)
        Kred = 0.299F / SrcDY
        Kgreen = 0.587F / SrcDY
        Kblue = 0.114F / SrcDY
        SrcPen = New Pen(Color.FromArgb(200, 120, 0), SrcH \ 100)
        ' ---------------------------------------------------------------------
        Spectrometer_SetScaleTrimParams()
        Spectrometer_ResetReference()
    End Sub

    Friend Sub Spectrometer_SetRunningModeParams()
        ShowDips = Form_Main.btn_Dips.Checked()
        ShowPeaks = Form_Main.btn_Peaks.Checked()
        UseColors = Form_Main.btn_Colors.Checked()
        TrimScale = Form_Main.btn_TrimScale.Checked()
        Filter = Form_Main.txt_Filter.NumericValueInteger
        KFilter = (100 - Filter) / 100.0F + 0.1F
        Speed = Form_Main.txt_Speed.NumericValueInteger
        KSpeed = Speed / 100.0F
    End Sub

    Friend Sub Spectrometer_SetScaleTrimParams()
        If NanometersMin < 50 Then NanometersMin = 50
        If NanometersMax > 2000 Then NanometersMax = 2000
        If NanometersMax < NanometersMin + 50 Then NanometersMax = NanometersMin + 50
        NanometersDelta = NanometersMax - NanometersMin
        ' ---------------------------------------------------------------------
        NmStart = NanometersMin + NanometersDelta * SrcX0 / SrcW
        NmEnd = NanometersMax - NanometersDelta * (1.0F - CSng(SrcX0 + SrcDX - 1) / SrcW)
        ' ---------------------------------------------------------------------
        NmStartDiv = 10 * CInt(NmStart / 10.0F)
        ' ---------------------------------------------------------------------
        SetNmCoeff()
    End Sub

    Private Sub Spectrometer_SetDestParams()
        DestW = DestPbox.Image.Width
        DestH = DestPbox.Image.Height
        DestRight = DestW - 1
        DestBottom = DestH - 2
        SetNmCoeff()
    End Sub

    Private Sub SetNmCoeff()
        If NmEnd > NmStart Then
            NmCoeff = DestW / (NmEnd - NmStart)
        Else
            NmCoeff = 1
        End If
    End Sub

    ' ================================================================================
    '  REFERENCE - SET RESET
    ' ================================================================================
    Private Reference(-1) As Single
    Friend Sub Spectrometer_SetReference()
        Reference = CType(SpecArrayFiltered.Clone, Single())
        For i As Int32 = 0 To SrcDX - 1
            If Reference(i) < 1 Then Reference(i) = 99999
        Next
        ReferenceScale = True
    End Sub
    Friend Sub Spectrometer_ResetReference()
        Form_Main.btn_Reference.Checked = False
        ReDim Reference(-1)
        ReferenceScale = False
    End Sub

    ' ================================================================================
    '  PROCESS CAPTURED IMAGE
    ' ================================================================================
    Friend Sub ProcessCapturedImage()
        ' -----------------------------------------------------------------------
        If Not Capture_NewImageIsReady Then Return
        Dim t As PrecisionTimer = New PrecisionTimer
        ' ----------------------------------------------------------------------- LOAD CAPTURED IMAGE
        SrcImage = Capture_Image
        If SrcImage Is Nothing Then Return
        ' ----------------------------------------------------------------------- CAPTURE IMAGE NOT READY
        Capture_NewImageIsReady = False
        ' ----------------------------------------------------------------------- FLIP
        If Flip Then
            SrcImage.RotateFlip(RotateFlipType.RotateNoneFlipX)
            'SrcImage.RotateFlip(RotateFlipType.RotateNoneFlipXY)
        Else
            'SrcImage.RotateFlip(RotateFlipType.RotateNoneFlipNone)
            'SrcImage.RotateFlip(RotateFlipType.RotateNoneFlipY)
        End If
        ' ----------------------------------------------------------------------- PREPARE BITMAP
        SrcBmp = CType(SrcImage, Bitmap)
        ' ----------------------------------------------------------------------- SET PARAMS
        If SrcImage.Width <> SrcW Or SrcImage.Height <> SrcH Then
            Spectrometer_SetSourceParams()
        End If
        ' ----------------------------------------------------------------------- EXTRACT SPECTRUM
        BitmapToSpectrum()
        ' ----------------------------------------------------------------------- SHOW AREA
        Dim SrcGraphics As Graphics = Graphics.FromImage(SrcBmp)
        SrcGraphics.DrawRectangle(SrcPen, SrcX0, SrcY0, SrcDX, SrcDY)
        ' ----------------------------------------------------------------------- SHOW SOURCE IMAGE
        Form_Main.PBox_Camera.Image = SrcImage
        'Form_Main.PBox_Camera.Image = CType(SrcImage.Clone, Image)
        ' ----------------------------------------------------------------------- IMAGE INFO
        Form_Main.Label_Resolution.Text = Capture_Image.Width.ToString & _
                                          " x " & Capture_Image.Height.ToString
        Form_Main.Label_FramesPerSec.Text = Capture_FramesPerSecond.ToString("0") & " fps"
        ' -----------------------------------------------------------------------
        ShowSpectrumGraph()
        ' ----------------------------------------------------------------------- 
        MeterBar1.SetValue(MaxValue / 256.0F)
        ' ----------------------------------------------------------------------- STATUS BAR INDICATIONS
        If Not CursorInside Then
            If MaxValue > 0.11 Then
                Dim nm As Int32 = CInt(X_To_Nanometers((MaxValueX * DestW) \ SrcDX))
                If nm <> MaxNanometers_OldValue Then
                    MaxNanometers_OldValue = nm
                    Form_Main.Label_MaxPeak.Text = "Max: " & nm.ToString & " nm"
                End If
            Else
                MaxNanometers_OldValue = -1
                Form_Main.Label_MaxPeak.Text = ""
            End If
        End If
        ' -----------------------------------------------------------------------
        Form_Main.Label_Millisec.Text = (t.GetTimeMicrosec / 1000.0F).ToString("0") & " mS"
    End Sub

    Private Sub BitmapToSpectrum()
        If SrcBmp Is Nothing Then Return
        Dim SourceData As BitmapData = SrcBmp.LockBits(New Rectangle(SrcX0, SrcY0, SrcDX, SrcDY), _
                                                       ImageLockMode.ReadWrite, _
                                                       PixelFormat.Format24bppRgb)
        Dim SourceStride As Int32 = SourceData.Stride
        Dim byteCount As Integer = (SourceData.Stride * SourceData.Height)
        Dim bmpBytes(byteCount - 1) As Byte
        Marshal.Copy(SourceData.Scan0, bmpBytes, 0, byteCount)
        SrcBmp.UnlockBits(SourceData)
        Dim sumr As Int32 = 0
        Dim sumg As Int32 = 0
        Dim sumb As Int32 = 0
        Dim disp As Integer
        Dim v As Single
        For x As Int32 = 0 To SrcDX - 1
            sumr = 0
            sumg = 0
            sumb = 0
            disp = x * 3
            For y As Int32 = 0 To SrcDY - 1
                sumr += bmpBytes(disp + 2)
                sumg += bmpBytes(disp + 1)
                sumb += bmpBytes(disp)
                disp = disp + SourceStride
            Next
            v = sumr * Kred + sumg * Kgreen + sumb * Kblue
            SpecArray(x) += (v - SpecArray(x)) * KSpeed
        Next
        AddFilter()
        AddReference()
    End Sub

    Private Sub AddFilter()
        MaxValue = 0.1
        Dim v, vnew As Single
        For i As Int32 = 0 To SrcDX - 1
            vnew = SpecArray(i)
            ' ----------------------------------------- filter
            v += (vnew - v) * KFilter
            ' ----------------------------------------- store filtered value
            SpecArrayFiltered(i) = v
        Next
        For i As Int32 = SrcDX - 1 To 0 Step -1
            vnew = SpecArray(i)
            ' ----------------------------------------- filter
            v += (vnew - v) * KFilter
            ' ----------------------------------------- add up and down filter passes
            SpecArrayFiltered(i) += v
            ' ----------------------------------------- update Max
            If SpecArrayFiltered(i) > MaxValue Then
                MaxValue = SpecArrayFiltered(i)
                MaxValueX = i
            End If
        Next
    End Sub

    Private Sub AddReference()
        If Reference.Length = 0 Then Return
        Dim v As Single
        For i As Int32 = 0 To SrcDX - 1
            v = SpecArrayFiltered(i)
            ' ----------------------------------------- reference
            v = v * 0.9F * MaxValue / Reference(i)
            If v > MaxValue Then v = MaxValue
            ' ----------------------------------------- store value
            SpecArrayFiltered(i) = v
        Next
    End Sub


    Friend Sub ShowSpectrumGraph()
        ' --------------------------------------------------------------------
        InitPictureboxImage(DestPbox)
        If DestPbox.Image Is Nothing Then Return
        If DestPbox.Image.Width <> DestW Or DestPbox.Image.Height <> DestH Then
            Spectrometer_SetDestParams()
            gfx = Graphics.FromImage(DestPbox.Image)
        End If
        ' --------------------------------------------------------------------
        gfx.Clear(Color.AliceBlue)
        Dim x As Single
        Dim y As Single
        Dim drawText As Boolean
        ' --------------------------------------------------------------------- scale X
        For i As Int32 = NmStartDiv To CInt(NmEnd) Step 10
            drawText = False
            x = (i - NmStart) * NmCoeff
            If i Mod 100 = 0 Then
                gfx.DrawLine(ScalePen1, x, 15, x, DestBottom)
                drawText = True
            ElseIf i Mod 50 = 0 Then
                gfx.DrawLine(ScalePen2, x, 15, x, DestBottom)
                If NmCoeff > 2 Then drawText = True
            Else
                gfx.DrawLine(ScalePen3, x, 15, x, DestBottom)
                If NmCoeff > 5 Then drawText = True
            End If
            If drawText Then
                gfx.DrawString(i.ToString, ScaleFont, Brushes.Black, x - 4, 1)
            End If
        Next
        ' --------------------------------------------------------------------- scale Y
        If ReferenceScale Then
            For i As Int32 = 0 To 110 Step 5
                y = DestBottom - i / 110.0F * (DestBottom - 15)
                If i = 100 Then
                    gfx.DrawLine(Pens.Orange, 0, y, DestRight, y)
                ElseIf i = 50 Then
                    gfx.DrawLine(Pens.Orange, 0, y, DestRight, y)
                ElseIf i Mod 10 = 0 Then
                    gfx.DrawLine(ScalePen2, 0, y, DestRight, y)
                Else
                    gfx.DrawLine(ScalePen3, 0, y, DestRight, y)
                End If
            Next
        Else
            For i As Int32 = 0 To 100 Step 5
                y = DestBottom - i / 100.0F * (DestBottom - 15)
                If i = 100 Then
                    gfx.DrawLine(Pens.YellowGreen, 0, y, DestRight, y)
                ElseIf i = 50 Then
                    gfx.DrawLine(Pens.YellowGreen, 0, y, DestRight, y)
                ElseIf i Mod 10 = 0 Then
                    gfx.DrawLine(ScalePen2, 0, y, DestRight, y)
                Else
                    gfx.DrawLine(ScalePen3, 0, y, DestRight, y)
                End If
            Next
        End If
        ' --------------------------------------------------------------------- graph vars
        Dim oldx As Single = -99
        Dim oldy As Single = 0
        kx = CSng(DestRight) / (SpecArrayFiltered.Length - 1)
        'ky = (DestBottom - 15) / (300.0F / Scale) ' Yscale manual
        ky = (DestBottom - 15) / MaxValue
        ' --------------------------------------------------------------------- graph color fill
        Dim xnew As Int32
        Dim xold As Int32
        Dim x3 As Int32
        Dim y1 As Single
        Dim y2 As Single
        If UseColors Then
            For i As Int32 = 0 To SpecArrayFiltered.Length - 1
                xnew = CInt(BinToX(i))
                If xnew = xold + 1 Then
                    y = SpecArrayFiltered(CInt(i)) * ky
                    If y > 2 Then
                        Pen_Graph.Color = WavelengthToColor(X_To_Nanometers(xnew))
                        gfx.DrawLine(Pen_Graph, xnew, DestBottom, xnew, DestBottom - y)
                    End If
                ElseIf xnew > xold Then
                    Pen_Graph.Color = WavelengthToColor(X_To_Nanometers(xnew))
                    y1 = SpecArrayFiltered(CInt(i - 1)) * ky
                    y2 = SpecArrayFiltered(CInt(i)) * ky
                    If y1 > 2 Or y2 > 2 Then
                        For x3 = xold + 1 To xnew
                            y = y1 + (y2 - y1) * (x3 - xold) / (xnew - xold)
                            gfx.DrawLine(Pen_Graph, x3, DestBottom, x3, DestBottom - y)
                        Next
                    End If
                End If
                xold = xnew
            Next
        End If
        ' --------------------------------------------------------------------- graph black line
        Pen_Graph.Color = Color.FromArgb(0, 70, 0)
        For i As Int32 = 0 To SpecArrayFiltered.Length - 1
            x = BinToX(i)
            y = DestBottom - SpecArrayFiltered(i) * ky
            gfx.DrawLine(Pen_Graph, oldx, oldy, x, y)
            'gfx.DrawLine(Pen_Graph, oldx + 1, oldy, x + 1, y) ' thick line
            If NmCoeff > 10 Then
                gfx.DrawRectangle(Pen_Graph, x - 1, y - 1, 3, 3) ' points
            End If
            oldx = x
            oldy = y
        Next
        ' --------------------------------------------------------------------- 
        If TrimScale Then
            MarkTrimPoint(TrimPoint1)
            MarkTrimPoint(TrimPoint2)
        End If
        MarkAllPeaks()
        ' --------------------------------------------------------------------- 
        DestPbox.Invalidate()
    End Sub

    Friend Function X_To_Nanometers(ByVal x As Single) As Single
        If NmCoeff = 0 Then Return 0
        Return NmStart + (x / NmCoeff)
    End Function

    Friend Function X_From_Nanometers(ByVal nm As Single) As Single
        Return (nm - NmStart) * NmCoeff
    End Function

    Friend Function BinToX(ByVal bin As Int32) As Single
        Return bin * kx
    End Function

    Friend Function XtoBin(ByVal x As Single) As Int32
        If kx = 0 Then Return 0
        Return CInt(x / kx)
    End Function


    Private Font_Peaks As Font = New Font("Arial", 9)
    Private Pen_Trim1 As Pen = New Pen(Color.White)

    Private Sub MarkTrimPoint(ByVal nm As Single)
        Pen_Trim1.DashStyle = DashStyle.Dot
        Dim x As Single
        Dim w As Int32 = 26
        x = X_From_Nanometers(nm)
        If x >= 0 And x < DestW Then
            Dim s As String = nm.ToString("0")
            If s.Length > 3 Then w = 34
            gfx.DrawLine(Pens.Black, x, 16, x, DestH)
            gfx.DrawLine(Pen_Trim1, x, 16, x, DestH)
            gfx.FillRectangle(Brushes.Yellow, x - 14, 0, w, 14)
            gfx.DrawRectangle(Pens.Red, x - 14, 0, w, 14)
            gfx.DrawString(s, Font_Peaks, Brushes.Black, x - 13, 0)
        End If
    End Sub

    Private Sub MarkPeak(ByVal bin As Int32, ByVal IsPeak As Boolean)
        Dim x As Single
        Dim y1 As Int32
        Dim y2 As Int32
        Dim w As Int32 = 26
        If bin > 0 Then
            x = BinToX(bin)
            If x >= 0 And x < DestW Then
                Dim s As String = X_To_Nanometers(x).ToString("0")
                If s.Length > 3 Then w = 34
                y1 = 15 + CInt((DestH - 15) * (1 - SpecArrayFiltered(bin) / MaxValue))
                If IsPeak Then
                    y2 = y1 - 20
                    If y2 < DestH - 50 Then y2 = DestH - 20
                    gfx.DrawLine(Pens.Red, x, y1 + 1, x, DestH)
                Else
                    y2 = 30
                    gfx.DrawLine(Pens.Green, x, 40, x, y1 - 3)
                End If
                gfx.FillRectangle(Brushes.Yellow, x - 14, y2, w, 14)
                gfx.DrawRectangle(Pens.Green, x - 14, y2, w, 14)
                gfx.DrawString(s, Font_Peaks, Brushes.Black, x - 13, y2)
            End If
        End If
    End Sub

    Private Sub MarkAllPeaks()
        Dim delta As Int32 = (20 * SrcDX) \ DestW
        If delta < 2 Then delta = 2
        Dim v As Single
        Dim valid As Boolean
        For i As Int32 = delta To SpecArrayFiltered.Length - delta - 1
            v = SpecArrayFiltered(i)
            If ShowPeaks Then
                If v > SpecArrayFiltered(i + 1) AndAlso _
                   v > SpecArrayFiltered(i - 1) AndAlso _
                   v * 100 > MaxValue Then
                    valid = True
                    For d As Int32 = 2 To delta
                        If v < SpecArrayFiltered(i + d) OrElse v < SpecArrayFiltered(i - d) Then
                            valid = False
                            Exit For
                        End If
                    Next
                    If valid Then MarkPeak(i, True)
                End If
            End If
            If ShowDips Then
                If v < SpecArrayFiltered(i + 1) AndAlso _
                   v < SpecArrayFiltered(i - 1) AndAlso _
                   v * 10000000 > MaxValue Then
                    valid = True
                    For d As Int32 = 2 To delta
                        If v > SpecArrayFiltered(i + d) OrElse v > SpecArrayFiltered(i - d) Then
                            valid = False
                            Exit For
                        End If
                    Next
                    If valid Then MarkPeak(i, False)
                End If
            End If
        Next
    End Sub

End Module
