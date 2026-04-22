Imports System
Imports System.IO
Imports Atalasoft.Imaging
Imports Atalasoft.Imaging.Codec
Imports Atalasoft.Imaging.Codec.Pdf
Imports Atalasoft.Imaging.ImageProcessing.Document
Imports Atalasoft.Imaging.ImageProcessing
Imports System.Windows.Forms
Imports Atalasoft.Imaging.ImageSources

Namespace PDFtoTIFF
    ''' <summary>
    ''' Summary description for Class1.
    ''' </summary>
    Friend Class Module1
        ''' <summary>
        ''' Converts any multipage PDF file into a multipage TIFF file
        ''' </summary>
        <STAThread()> _
        Public Shared Sub Main(ByVal args As String())
            DoAboutSplash()

            Console.WriteLine("PdfToTiffConverter starting")

            ' Defining a PDF decoder that we will add to our registered decoders
            Dim pdfDec As New PdfDecoder()
            ' Setting the resolution to 200DPI which is common for TIFF files
            pdfDec.Resolution = 200
            ' Adding the decoder to our registed decoders makes handling of PDFs automatic
            RegisteredDecoders.Decoders.Add(pdfDec)

            Console.WriteLine("Select the PDF File to convert to TIFF:")
            Dim imgPath As String = GetWorkingDir()
            Dim inFile As String = imgPath & Convert.ToString("target.pdf")

            Using dlg As New OpenFileDialog()
                dlg.InitialDirectory = imgPath
                dlg.FileName = Path.GetFileName(inFile)
                dlg.Filter = "Adobe Portable Document Format (PDF)|*.pdf;"
                If dlg.ShowDialog() = DialogResult.OK Then
                    inFile = dlg.FileName
                Else
                    inFile = Nothing
                End If
            End Using


            If inFile Is Nothing Then
                Console.WriteLine("No input file SelectedOperation canceled")
            Else
                Dim outFile As String = System.IO.Path.ChangeExtension(inFile, ".tif")

                Console.WriteLine(Convert.ToString("  inFile: ") & inFile)

                Console.WriteLine("Please select a location to save the outgoing TIFF to:")
                Using dlg As New SaveFileDialog()
                    dlg.Title = "Select Location to Save output TIFF to"
                    dlg.Filter = "Tagged Image File Format (TIFF)|*.tif;"
                    dlg.DefaultExt = ".tif"
                    dlg.InitialDirectory = Path.GetDirectoryName(inFile)
                    dlg.FileName = Path.GetFileName(outFile)

                    If dlg.ShowDialog() = DialogResult.OK Then
                        outFile = dlg.FileName
                    Else
                        ' force an error to stop the app
                        outFile = Nothing
                    End If
                End Using

                Console.WriteLine(Convert.ToString("  outFile: ") & outFile)

                If outFile Is Nothing Then
                    Console.WriteLine("No Output selcted: Operation canceled")
                Else

                    Console.WriteLine("Converting file...")

                    ' start timer
                    Dim tick1 As Integer = System.Environment.TickCount

                    ' Do the conversion
                    If File.Exists(inFile) Then
                        ConvertPdfToTiff(inFile, outFile)
                    Else
                        Console.WriteLine("file not found... doing nothing")
                    End If

                    ' finish timer
                    Dim tick4 As Integer = System.Environment.TickCount

                    Console.WriteLine("Conversion complete: Total time " + (tick4 - tick1).ToString() + " ms")
                End If
            End If

            Console.WriteLine("PdfToTiffConverter finished... press ENTER to quit")
            Console.ReadLine()
        End Sub


        ''' <summary>
        ''' Given the filename of a tiff file as input and a pdf file as output, convert the
        ''' incoming tiff to a SingleImageOnly PDF
        ''' 
        ''' This demonstrates the most memory-efficient way to do the conversion.
        ''' </summary>
        ''' <param name="inFile">A tiff file to use as the input</param>
        ''' <param name="outFile">A filename to call the outgoing PDF (note that it will delete any existing file by that name first.</param>
        Private Shared Sub ConvertPdfToTiff(ByVal inFile As String, ByVal outFile As String)
            ' Make sure we get rid of the output file if it already exists
            If File.Exists(outFile) Then
                File.Delete(outFile)
            End If

            ' Create your encoder
            Dim tiffEnc As New TiffEncoder()

            ' For Jpeg compression (not Jpeg2000), you can use this value to request 
            ' higher compression or quality... the higher the number, the better the 
            ' quality, but the less effective the compression. 80 is the default value/
            tiffEnc.JpegQuality = 75

            ' set up an event handler that will pop for each image to let you determine the compression
            AddHandler tiffEnc.SetEncoderCompression, AddressOf tiffEnc_SetEncoderCompression


            ' Reading this direct from a FileSystemImageSource will be much more efficient than using an ImageCollection
            Using inStream As FileStream = New FileStream(inFile, FileMode.Open, FileAccess.Read, FileShare.Read)


                Using imgSrc As PdfImageSource = New PdfImageSource(inStream) With {.Resolution = 200}
                    Using s As Stream = File.OpenWrite(outFile)
                        ' The magic of using an ImageSource and Stream with the pdfEncoder is 
                        ' that only the active portion of a given image is in memory at one time
                        ' so it's very memory efficient.
                        '
                        ' This effectively does   while(imgSrc.HasMoreImages()) { AtalaImage img = imgSrc.AcquireNext(); ... }
                        '
                        ' Each page willl trigger a new tiffEnc_SetEncoderCompression event 
                        ' where you can choose the type of compression you want to apply on a page-by-page basis
                        tiffEnc.Save(s, imgSrc, Nothing)
                    End Using
                End Using
            End Using
            ' just being polite and cleaning up after ourselves
            tiffEnc = Nothing
        End Sub


        ''' <summary>
        ''' Intelligently selects the appropriate form of compression to apply to the current page
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="e"></param>
        Private Shared Sub tiffEnc_SetEncoderCompression(ByVal sender As Object, ByVal e As EncoderCompressionEventArgs)
            If e.Image.PixelFormat = PixelFormat.Pixel1bppIndexed Then
                ' Here, we've chosen Group4FaxEncoding as our B&W conversion.
                ' Group3FaxEncoding may be a viable alternative for you
                'e.Compression = new TiffCodecCompression(TiffCompression.Group3FaxEncoding);
                ' ModifiedHuffman may be a viable alternative for you
                'e.Compression = new TiffCodecCompression(TiffCompression.ModifiedHuffman);
                e.Compression = New TiffCodecCompression(TiffCompression.Group4FaxEncoding)
            ElseIf e.Image.PixelFormat = PixelFormat.Pixel8bppGrayscale Then
                ' LZW may do a better job on Graysale than Jpeg Compression, and without the resulting loss
                e.Compression = New TiffCodecCompression(TiffCompression.Lzw)
            ElseIf e.Image.PixelFormat = PixelFormat.Pixel24bppBgr Then
                ' Here, we've chosen JpegCompression as our Color conversion.
                ' If quality is much more important than compression, you could use LZW or Deflate here
                'e.Compression = new TiffCodecCompression(TiffCompression.Lzw);
                'e.Compression = new TiffCodecCompression(TiffCompression.Deflate);
                e.Compression = New TiffCodecCompression(TiffCompression.JpegCompression)
            Else
                ' Fallback method in case the pixelFormat isn't one of the ones defined above
                ' LZW may be a viable alternative for you
                'e.Compression = new TiffCodecCompression(TiffCompression.Lzw);
                e.Compression = New TiffCodecCompression(TiffCompression.Deflate)
            End If
        End Sub

        ''' <summary>
        ''' Outputs the "About" spash info
        ''' </summary>
        Private Shared Sub DoAboutSplash()
            Console.WriteLine("PDFtoTIFF Demo")
            Console.WriteLine()
            Console.WriteLine("***************************************************************************")
            Console.WriteLine("A very simple console app that converts a PDF file into a TIFF by")
            Console.WriteLine("using in a memory-efficient way using FileSystemImageSource and")
            Console.WriteLine("our PdfRasterizer addon.")
            Console.WriteLine()
            Console.WriteLine("Who says you always need a viewer in an imaging application?")
            Console.WriteLine()
            Console.WriteLine("This console app uses our PdfRasterizer to convert a PDF to a TIFF.")
            Console.WriteLine("This approach can easily be adapted to services or plumbed in to")
            Console.WriteLine("batch-based processing.")
            Console.WriteLine()
            Console.WriteLine("By setting a handler for TiffEncoder.SetEncoderCompression, we are ")
            Console.WriteLine("able to dynamically select the most appropriate form of image ")
            Console.WriteLine("compression to apply, based on the PixelFormat (color depth) of each page")
            Console.WriteLine()
            Console.WriteLine()
            Console.WriteLine("Download the DotImage SDK at:")
            Console.WriteLine("     http://www.atalasoft.com/products/download/dotimage")
            Console.WriteLine()
            Console.WriteLine("Download the DotImage API Help Installers at:")
            Console.WriteLine("     http://www.atalasoft.com/support/dotimage/help/install")
            Console.WriteLine()
            Console.WriteLine("Download the full sources for this demo at:")
            Console.WriteLine("     http://www.atalasoft.com/KB/article.aspx?id=10412")
            Console.WriteLine("***************************************************************************")
            Console.WriteLine()
        End Sub


        ''' <summary>
        ''' Convenience method to get the root directory of the project - really only useful for debugging
        ''' </summary>
        ''' <returns></returns>
        Private Shared Function GetWorkingDir() As String
            Dim cwd As String = System.IO.Directory.GetCurrentDirectory()
            'Console.WriteLine("cwd is '{0}'", cwd);

            If cwd.EndsWith("\bin\Debug") Then
                'Console.WriteLine("updated cwd is '{0}'", cwd);
                cwd = cwd.Replace("\bin\Debug", "\..\..\")
            ElseIf cwd.EndsWith("\bin") Then
                cwd = cwd.Replace("\bin", "\..\")
            End If
            Return cwd
        End Function
    End Class
End Namespace
