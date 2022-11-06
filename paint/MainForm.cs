/*
Eraser function: I was not able to add a click and drag erase function
(just drawing with a transparent brush does not replace the pixels), so
I made it so it works like it did in old versions of Windows Paint, where
if you drag the eraser it erases circles, but not continuously.

Undo/Redo function: Simply undos last action. The bitmap history is stored in a list
and when you undo some change, you go back one element, and when you redo,
you go one action into the future. If you do another action,
it overwrites the future actions of the current bitmap, so they are lost
and it is impossible to redo them.

Save function: Saves the current bitmap as a png file in the current project
folder.

Hold Shift to draw line: Holding shift will make it so that you can draw and draw a 
line from where you first pressed the mouse button to the position where you release it.

Adding many function made the program run slowlier (especially because of
many if/else statements running every mouse move). I tried thinking of a solution
to that but could not think of anything.
*/

// MainForm.cs
using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;

namespace paint
{
    class DrawableImage : Drawable
    {
        private Bitmap bitmap;
        public Color CurrentColor { get; set; }
        public int BrushSize { get; set; }
        private PointF lastMouseLocation;
        private PointF defaultMouseLocation = new PointF(0, 0);
        private Pen drawingPen;
        private List<Bitmap> history = new List<Bitmap>(); // list that contains bitmap history
        private int currentHistory = 0; // current index of bitmap in history

        // Resets the canvas
        public void ClearCanvas() {
            using (Graphics g = new Graphics(bitmap)) 
            {
                g.Clear();
            }
            Invalidate();
        }

        // undos an action by restoring the previous bitmap from history (if possible)
        public void Undo() {
            if (currentHistory > 0) {
                bitmap = new Bitmap(history[--currentHistory]);
            }

            Invalidate();
        }

        // redos an action by restoring the next bitmap from history (if possible)
        public void Redo() {
            if (currentHistory < history.Count - 1) {
                bitmap = new Bitmap(history[++currentHistory]);
            }

            Invalidate();
        }

        // saves the current bitmap as a png file in the current project folder
        public void SaveImage() {
            bitmap.Save("image.png", Eto.Drawing.ImageFormat.Png);
            Invalidate();
        }

        public DrawableImage()
        {
            bitmap = new Bitmap(600, 400, PixelFormat.Format32bppRgba);
            history.Add(new Bitmap(600, 400, PixelFormat.Format32bppRgba));

            Paint += (s, pe) =>
            {
                pe.Graphics.DrawImage(bitmap, new PointF(0, 0));
            };

            MouseDown += (s, me) =>
            {
                SizeF circleSize = new SizeF(BrushSize, BrushSize);
                PointF center = me.Location;
                lastMouseLocation = me.Location;

                drawingPen = new Pen(CurrentColor, BrushSize) { LineCap = PenLineCap.Round };
            
                // if color is argb(0, 0, 0, 0), treat the brush as an eraser
                // basically, we want to clear a circle centered at where we clicked
                // so we set a gpath there with the width of the brush
                if (CurrentColor == Color.FromArgb(0, 0, 0, 0)) {
                    using (Graphics g = new Graphics(bitmap))
                    {
                        IGraphicsPath gpath = new GraphicsPath();
                        gpath.AddEllipse(new RectangleF(center - 0.5F * circleSize, center + 0.5F * circleSize));
                        g.SetClip(gpath);
                        g.Clear();
                        g.ResetClip();
                    }
                } else if (me.Modifiers.HasFlag(Keys.Shift)) { // if holding shift
                    history.Add(new Bitmap(bitmap));           // add current bitmap to history
                                                               // and we'll update the line to be drawn on mouse move
                    using (Graphics g = new Graphics(bitmap)) {
                        g.FillEllipse(CurrentColor, new RectangleF(center - 0.5F * circleSize, center + 0.5F * circleSize));
                    }
                } else using (Graphics g = new Graphics(bitmap)) {
                    g.FillEllipse(CurrentColor, new RectangleF(center - 0.5F * circleSize, center + 0.5F * circleSize));
                }

                Invalidate();
            };

            MouseMove += (s, me) =>
            {
                if ( me.Buttons.HasFlag( MouseButtons.Primary ) && (lastMouseLocation != defaultMouseLocation)) {
                    // if holding shift, it updates the position of where the line is going
                    // to be; line will be drawn on mouse up of shift up
                    if (me.Modifiers.HasFlag(Keys.Shift)) {
                        bitmap = new Bitmap(history[history.Count - 1]);

                        using (Graphics g = new Graphics(bitmap)) {
                            g.DrawLine(drawingPen, lastMouseLocation, me.Location);
                        }

                    } else {
                        using (Graphics g = new Graphics(bitmap))
                        {
                            g.DrawLine(drawingPen, lastMouseLocation, me.Location);
                        }

                        lastMouseLocation = me.Location;
                    }
                    
                    Invalidate();   
                }
            };

            MouseUp += (s, e) => 
            {
                lastMouseLocation = defaultMouseLocation;

                // whenever mouse goes up, we delete all changes that occured after
                // current bitmap in history
                while (currentHistory != history.Count - 1) history.RemoveAt(history.Count - 1);

                // and then add changed bitmap to new history
                history.Add(new Bitmap(bitmap));
                currentHistory++;
            };

            SizeChanged += (s, e) =>
            {
                if (bitmap.Height < ClientSize.Height || bitmap.Width < ClientSize.Width)
                {
                    Bitmap oldImg = bitmap;

                    Size newSize = Size.Max(ClientSize, oldImg.Size);
                    newSize += Size.Max(newSize / 2, new Size(1, 1));
                    Bitmap newImg = new Bitmap(newSize, PixelFormat.Format32bppRgba);
                    using (Graphics g = new Graphics(newImg))
                    {
                        g.DrawImage(oldImg, new PointF(0, 0));
                    }
                    bitmap = newImg;
                    oldImg.Dispose();
                    Invalidate();
                }
            };
        }

    }

    public class MainForm : Form
    {
        public MainForm()
        {
            Title = "PaintClone";
            MinimumSize = new Size(650, 400);

            // Color Picker
            ColorPicker colorPicker = new ColorPicker { Value = Colors.Red };
            
            // Brush Size selector
            NumericStepper sizeSelector = new NumericStepper
            {
                MinValue = 1,   
                MaxValue = 150, 
                Increment = 1, 
                Value = 5
            };

            DrawableImage d = new DrawableImage { CurrentColor = colorPicker.Value, BrushSize = (int)sizeSelector.Value };
            colorPicker.ValueChanged += (s, e) =>
            {
                d.CurrentColor = colorPicker.Value;
            };

            // TOOLBAR ITEMS START ----------------------------------
            // Clear Button
            Button clearButton = new Button {Text = "Clear", Width = 45};
            clearButton.Click += (s, e) =>
            {
                d.ClearCanvas();
            };

            Button rubberButton = new Button {Text = "Eraser", Width = 45};
            rubberButton.Click += (s, e) =>
            {
                d.CurrentColor = Color.FromArgb(0, 0, 0, 0);
            };

            sizeSelector.ValueChanged += (s, e) =>
            {
                d.BrushSize = (int)sizeSelector.Value;
            };

            Button undoButton = new Button {Text = "Undo", Width = 45};
            undoButton.Click += (s, e) => 
            {
                d.Undo();
            };

            Button redoButton = new Button {Text = "Redo", Width = 45};
            redoButton.Click += (s, e) => 
            {
                d.Redo();
            };

            Button saveButton = new Button {Text = "Save", Width = 45};
            saveButton.Click += (s, e) => 
            {
                d.SaveImage();
            };
            // TOOLBAR ITEMS END ----------------------------------

            // Toolbar definition
            StackLayout ToolBar = new StackLayout
            {
                Padding = 5,
                Spacing = 5,
                Orientation = Orientation.Horizontal,
                VerticalContentAlignment = VerticalAlignment.Center,
                Items = {
                    new StackLayoutItem( colorPicker, true ),
                    sizeSelector,
                    rubberButton,
                    clearButton,
                    undoButton,
                    redoButton,
                    saveButton
                }
            };

            // App content
            Content = new StackLayout
            {
                Padding = 5,
                Spacing = 5,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items = {
                    ToolBar,
                    new StackLayoutItem( d, true ),
                }
            };
            Menu = new MenuBar();
        }
    }
}