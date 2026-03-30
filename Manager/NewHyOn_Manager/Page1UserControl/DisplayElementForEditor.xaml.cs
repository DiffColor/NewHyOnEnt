using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using TurtleTools;


namespace AndoW_Manager
{
    /// <summary>
    /// DisplayElementForEditor.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DisplayElementForEditor : UserControl
    {
        /////////////////////////////////////////////////////////////////
        // Commaon Property
        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();

        public bool g_IsSelected = false;

        public Size g_PrevSize = new Size();

        public ElementPosClass g_PrevPos = new ElementPosClass();

        public DisplayElementForEditor()
        {
            InitializeComponent();
            InitEventHandler();
            RefreshOverlayScale();
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(MoveTopRectangle_PreviewMouseLeftButtonDown);
            this.resizingSpotRect.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(ResizeRectangle_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += DisplayElementForEditor_PreviewMouseMove;
            this.MouseLeave += DisplayElementForEditor_MouseLeave;
            this.PreviewMouseUp += DisplayElementForEditor_PreviewMouseUp;

            ElementDelete.Click += ElementDelete_Click;
            MenuFullScreen.Click += MenuFullScreen_Click;
            MenuSetManualPos.Click += MenuSetManualPos_Click;

            MenuBringToFront.Click += MenuBringToFront_Click;
            MenuBringToBack.Click += MenuBringToBack_Click;
            MenuBringToFrontOneStep.Click += MenuBringToFrontOneStep_Click;
            MenuBringToBackOneStep.Click += MenuBringToBackOneStep_Click;
            MuteToggleButton.Click += MuteToggleButton_Click;
        }

        void MenuSetManualPos_Click(object sender, RoutedEventArgs e)
        {
            ManualPosWindow mpw = new ManualPosWindow(this);
            mpw.ShowDialog();
        }

        public void SetManualPosData(double x, double y, double w, double h)
        {
            Page1.Instance.NeedGuideCheckBox.IsChecked = false;

            this.Width = w;
            this.Height = h;

            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);

            this.g_ElementInfoClass.EIF_PosTop = x;
            this.g_ElementInfoClass.EIF_PosLeft = y;

            UpdateElementLandSizeAndPos();
        }

        void MenuBringToBackOneStep_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToBackOneStepByElementName(this.Name);
        }

        void MenuBringToFrontOneStep_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToFrontOneStepByElementName(this.Name);
        }

        void MenuBringToBack_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToBackByElementName(this.Name);
        }

        void MenuBringToFront_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToFrontByElementName(this.Name);
        }

        void MenuFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if(Page1.Instance.g_DspElmtList.Count > 1)
            {
                MessageTools.ShowMessageBox("미디어 객체는 겹칠 수 없습니다.", "확인");
                return;
            }

            this.Width = 1920;
            this.Height = 1080;

            Canvas.SetTop(this, 0);
            Canvas.SetLeft(this, 0);

            this.g_ElementInfoClass.EIF_PosTop = 0;
            this.g_ElementInfoClass.EIF_PosLeft = 0;

            this.g_ElementInfoClass.EIF_RowVal = 0;
            this.g_ElementInfoClass.EIF_RowSpanVal = 24;
            this.g_ElementInfoClass.EIF_ColVal = 0;
            this.g_ElementInfoClass.EIF_ColSpanVal = 24;

            if (MainWindow.Instance.isPortraitEditor)
                WindowTools.ConvertInScaledUserCtrl(this, MainWindow.Instance.g_wPortScale, MainWindow.Instance.g_hPortScale);

            UpdateElementLandSizeAndPos();
        }
        
        public void UpdateZidxInfo(int paramIdx)
        {
            this.g_ElementInfoClass.EIF_ZIndex = paramIdx;
        }

        void ElementDelete_Click(object sender, RoutedEventArgs e)        
        {
            if (MessageTools.ShowMessageBox("선택한 객체를 삭제하시겠습니까?", "예", "아니오") == true)
            {
                Page1.Instance.DeleteObjectByName(g_ElementInfoClass.EIF_Name);
            }
        }

        public void UpdateElemenetnInfoCls(ElementInfoClass paramCls)
        {
            g_ElementInfoClass.CopyData(paramCls);
            DisplayContentsListCount();
            RefreshOverlayScale();
        }

        void MuteToggleButton_Click(object sender, RoutedEventArgs e)
        {
            g_ElementInfoClass.EIF_IsMuted = !g_ElementInfoClass.EIF_IsMuted;
            DisplayContentsListCount();
            ChooseThisElement();
        }

        void DisplayElementForEditor_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMuteToggleInteraction(e.OriginalSource))
            {
                this.ReleaseMouseCapture();
                return;
            }

            if (Page1.Instance.CheckStackObjects(Canvas.GetLeft(this), Canvas.GetTop(this), this.ActualWidth, this.ActualHeight, this.g_ElementInfoClass.EIF_Name))
            {
                Canvas.SetLeft(this, g_PrevPos.Left);
                Canvas.SetTop(this, g_PrevPos.Top);
                this.Width = g_PrevPos.Width;
                this.Height = g_PrevPos.Height;

                MessageTools.ShowMessageBox("미디어 객체는 겹칠 수 없습니다.", "확인");
            } 
            else 
                Reposition();
            
            this.ReleaseMouseCapture();
        }

        public void Reposition()
        {
            if (Page1.Instance.LockCheckBox.IsChecked == true) return;

            if (Page1.Instance.NeedGuideCheckBox.IsChecked == true)
            {
                UpdateElementSizeAndPosThroughGridBase();
            }
            else
            {
                UpdateElementSizeAndPosNoGridBase();
            }
        }

        public void UpdateElementSizeAndPosNoGridBase()
        {
            double leftVal = Canvas.GetLeft(this);
            double topVal = Canvas.GetTop(this);

            this.g_ElementInfoClass.EIF_PosLeft = leftVal;
            this.g_ElementInfoClass.EIF_PosTop = topVal;

            ////////////////////////////////////////////////////////////////////////
            // 2. 그다음엔 사이즈를 잡는다.

            this.g_ElementInfoClass.EIF_Width = g_PrevSize.Width = Width;
            this.g_ElementInfoClass.EIF_Height = g_PrevSize.Height = Height;

            SetBackOversized(leftVal, topVal);

            UpdateElementLandSizeAndPos();
        }


        public void UpdateElementSizeAndPosThroughGridBase()
        {
            double unitWidth = Page1.Instance.GuideGrid.ColumnDefinitions[0].ActualWidth;
            double unitHeight = Page1.Instance.GuideGrid.RowDefinitions[0].ActualHeight;

            ////////////////////////////////////////////////////////////////////////
            // 1. 위치부터 잡고 간다.
            int row1 = 0;
            int col1 = 0;

            double leftVal = Canvas.GetLeft(this);
            double topVal = Canvas.GetTop(this);

            GetPosColumn(new Point(leftVal, topVal), out row1, out col1);

            double posX = unitWidth * col1;
            double posY = unitHeight * row1;

            if (col1 < 0)
            {
                posX = 0;
                col1 = 0;
            }

            if (row1 < 0)
            {
                posY = 0;
                row1 = 0;
            }

            Canvas.SetLeft(this, posX);
            Canvas.SetTop(this, posY);

            this.g_ElementInfoClass.EIF_RowVal = row1;
            this.g_ElementInfoClass.EIF_ColVal = col1;

            this.g_ElementInfoClass.EIF_PosLeft = posX;
            this.g_ElementInfoClass.EIF_PosTop = posY;

            ////////////////////////////////////////////////////////////////////////
            // 2. 그다음엔 사이즈를 잡는다.

            SetBackOversized(posX, posY);

            int row2 = 0;
            int col2 = 0;

            if (this.ActualWidth == 0 || this.ActualHeight == 0)   // 처음 생성될때는 실제크기가 빵이다.
            {
                GetSizeColumns(new Point(leftVal + 800, topVal + 450), out row2, out col2);  //800,  450 은 처음 생설될때 임의로 정해준크기다.
            }
            else
            {
                GetSizeColumns(new Point(leftVal + this.ActualWidth, topVal + this.ActualHeight), out row2, out col2);

                row2++;
                col2++;
            }

            int gapX = col2 - col1;
            int gapY = row2 - row1;

            this.Width = g_PrevSize.Width = unitWidth * gapX;
            this.Height = g_PrevSize.Height = unitHeight * gapY;

            this.g_ElementInfoClass.EIF_Width = Width;
            this.g_ElementInfoClass.EIF_Height = Height;
            
            this.g_ElementInfoClass.EIF_ColSpanVal = gapX;
            this.g_ElementInfoClass.EIF_RowSpanVal = gapY;

            UpdateElementLandSizeAndPos();
        }

        void SetBackOversized(double posX, double posY)
        {
            if (posX < 0)
            {
                Canvas.SetLeft(this, 0);
            }

            if (posY < 0)
            {
                Canvas.SetTop(this, 0);
            }

            if (posX + Width > Page1.Instance.DesignerCanvas.ActualWidth)
            {
                if (Page1.Instance.g_IsSelecteControlResizing) 
                    Width = Page1.Instance.DesignerCanvas.ActualWidth - posX;
                else 
                    Canvas.SetLeft(this, Page1.Instance.DesignerCanvas.ActualWidth - Width);
            }
            if (posY + Height > Page1.Instance.DesignerCanvas.ActualHeight)
            {
                if (Page1.Instance.g_IsSelecteControlResizing)
                    Height = Page1.Instance.DesignerCanvas.ActualHeight - posY;
                else
                    Canvas.SetTop(this, Page1.Instance.DesignerCanvas.ActualHeight - Height);
            }
        }

        public void GetPosColumn(Point position, out int row, out int column)
        {
            column = 0;
            double total = 0;
            foreach (ColumnDefinition clm in Page1.Instance.GuideGrid.ColumnDefinitions)
            {
                if (position.X < total+clm.ActualWidth/2)
                {
                    break;
                }
                column++;
                total += clm.ActualWidth;
            }

            row = 0;
            total = 0;
            foreach (RowDefinition rowDef in Page1.Instance.GuideGrid.RowDefinitions)
            {
                if (position.Y < total+rowDef.ActualHeight/2)
                {
                    break;
                }
                row++;
                total += rowDef.ActualHeight;
            }
        }

        public void GetSizeColumns(Point position, out int row, out int column)
        {
            column = -1;
            double total = 0;
            foreach (ColumnDefinition clm in Page1.Instance.GuideGrid.ColumnDefinitions)
            {
                if (position.X < total + clm.ActualWidth/2)
                {
                    break;
                }
                column++;
                total += clm.ActualWidth;
            }

            row = -1;
            total = 0;
            foreach (RowDefinition rowDef in Page1.Instance.GuideGrid.RowDefinitions)
            {
                if (position.Y < total + rowDef.ActualHeight/2)
                {
                    break;
                }
                row++;
                total += rowDef.ActualHeight;
            }
        }

        void DisplayElementForEditor_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == true)
            {
                BoundaryBorder2.BorderBrush = ColorTools.GetSolidBrushByColorString("#FF17389E");
                TextDisplayName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFF4848");
            }
            else
            {
                BoundaryBorder2.BorderBrush = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                TextDisplayName.Foreground = new SolidColorBrush(Colors.White);
            }
            IsFirstMouseEnter = false;
        }

        bool IsFirstMouseEnter = false;
        void DisplayElementForEditor_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (IsFirstMouseEnter == false)
            {
                BoundaryBorder2.BorderBrush = ColorTools.GetSolidBrushByColorString("#FFFF4848");
                TextDisplayName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFF4848");
                IsFirstMouseEnter = true;
            }
        }

        public void UpdateElementLandSizeAndPos()
        {

            double leftVal = Canvas.GetLeft(this);
            double topVal =  Canvas.GetTop(this);

            SetBackOversized(leftVal, topVal);

            g_ElementInfoClass.EIF_PosLeft = leftVal;
            g_ElementInfoClass.EIF_PosTop = topVal;
            g_ElementInfoClass.EIF_Width = Width;
            g_ElementInfoClass.EIF_Height = Height;

            //ConvertToLandData();
        }

        //public void ConvertToLandData()
        //{
        //    if (MainWindow.Instance.isPortraitEditor)
        //    {
        //        g_ElementInfoClass.EIF_PosLeft *= MainWindow.Instance.g_wLandScale;
        //        g_ElementInfoClass.EIF_PosTop *= MainWindow.Instance.g_hLandScale;
        //        g_ElementInfoClass.EIF_Width *= MainWindow.Instance.g_wLandScale;
        //        g_ElementInfoClass.EIF_Height *= MainWindow.Instance.g_hLandScale;
        //    }
        //}

        public void SetFreeThisElementFromSelecting()
        {
            g_IsSelected = false;
            BoundaryBorder2.BorderBrush = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
            TextDisplayName.Foreground = new SolidColorBrush(Colors.White);
        }

        void ResizeRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.g_IsSelecteControlResizing = true;
        }

        void MoveBottomRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.SetCurrentSelectedElementMovable(true, e.GetPosition(this));
        }

        void MoveTopRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMuteToggleInteraction(e.OriginalSource))
            {
                return;
            }

            Page1.Instance.SetCurrentSelectedElementMovable(true, e.GetPosition(this));
            ChooseThisElement();

            if (g_IsSelected == false)
            {
                g_IsSelected = true;
                BoundaryBorder2.BorderBrush = ColorTools.GetSolidBrushByColorString("#FFEA6868"); 
                TextDisplayName.Foreground = ColorTools.GetSolidBrushByColorString("#FFEF0F0F");
            }
            else
            {
                g_IsSelected = false;
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.DimGray);
                TextDisplayName.Foreground = new SolidColorBrush(Colors.White);
            }

            Mouse.Capture(this);

            g_PrevPos.Left = Canvas.GetLeft(this);
            g_PrevPos.Top = Canvas.GetTop(this);
            g_PrevPos.Width = this.ActualWidth;
            g_PrevPos.Height = this.ActualHeight;
        }

        bool IsMuteToggleInteraction(object originalSource)
        {
            DependencyObject current = originalSource as DependencyObject;
            while (current != null)
            {
                if (current == MuteToggleButton)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        void contentsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ChooseThisElement();
        }
      
        public void ChooseThisElement()
        {
            g_PrevSize.Width = this.ActualWidth;
            g_PrevSize.Height = this.ActualHeight;

            Page1.Instance.SetCurrentSelectedObjectName(this.Name, TextDisplayName.Text, this.g_ElementInfoClass);
            Page1.Instance.SetCurrentSelectedElement(this);
            Page1.Instance.ReleaseSelectedObjectExceptSelectedObject();
        }

        public void AddContentInfoCls(ContentsInfoClass paramCls)
        {
            ContentsInfoClass tmpCls = new ContentsInfoClass();
            tmpCls.CopyData(paramCls);

            this.g_ElementInfoClass.EIF_ContentsInfoClassList.Add(tmpCls);
            //Page1.Instance.SetCurrentSelectedObjectName(this.Name, TextDisplayName.Text, this.g_ElementInfoClass);

            DisplayContentsListCount();
        }

        public void RefreshContentsInfoList(List<ContentsInfoClass> paramList)
        {
            if (paramList.Count > 0)
            {
                this.g_ElementInfoClass.EIF_ContentsInfoClassList.Clear();

                foreach (ContentsInfoClass item in paramList)
                {
                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(item);
                    this.g_ElementInfoClass.EIF_ContentsInfoClassList.Add(tmpCls);
                }
                
            }

            DisplayContentsListCount();
            ChooseThisElement();
        
        }

        public void DisplayContentsListCount()
        {
            TextInputChannelName.Text = string.Format("Contents: {0}ea", g_ElementInfoClass.EIF_ContentsInfoClassList.Count);
            bool showMuteToggle = g_ElementInfoClass.EIF_Type == DisplayType.Media.ToString();
            MuteToggleButton.Visibility = showMuteToggle ? Visibility.Visible : Visibility.Collapsed;
            MuteToggleButton.ToolTip = g_ElementInfoClass.EIF_IsMuted ? "현재 음소거 상태" : "현재 소리 출력 상태";
            MuteToggleIcon.Kind = g_ElementInfoClass.EIF_IsMuted ? PackIconKind.VolumeOff : PackIconKind.VolumeHigh;
            MuteToggleIcon.Foreground = g_ElementInfoClass.EIF_IsMuted ? new SolidColorBrush(Color.FromRgb(0x17, 0x38, 0x9E)) : new SolidColorBrush(Color.FromRgb(0x1C, 0x8A, 0x4B));
            MuteToggleBadge.BorderBrush = g_ElementInfoClass.EIF_IsMuted ? new SolidColorBrush(Color.FromArgb(0x66, 0x3F, 0x63, 0xB5)) : new SolidColorBrush(Color.FromArgb(0x66, 0x1C, 0x8A, 0x4B));
            MuteToggleBadge.Background = g_ElementInfoClass.EIF_IsMuted ? new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF)) : new SolidColorBrush(Color.FromArgb(0xE6, 0xF2, 0xFB, 0xF5));
        }

        public void RefreshOverlayScale()
        {
            double scaleX = 1.0;
            double scaleY = 1.0;

            Page1 page = Page1.Instance;
            if (page == null)
            {
                return;
            }

            ScaleTransform canvasScale = Page1.Instance?.DesignerCanvas?.RenderTransform as ScaleTransform;
            if (canvasScale != null)
            {
                if (Math.Abs(canvasScale.ScaleX) > 0.0001)
                {
                    scaleX = canvasScale.ScaleX;
                }

                if (Math.Abs(canvasScale.ScaleY) > 0.0001)
                {
                    scaleY = canvasScale.ScaleY;
                }
            }

            double designWidth = Math.Max(1.0, page.DesignGrid.Width);
            double designHeight = Math.Max(1.0, page.DesignGrid.Height);
            double viewboxScaleX = page.DesignViewBox.ActualWidth > 0 ? page.DesignViewBox.ActualWidth / designWidth : 1.0;
            double viewboxScaleY = page.DesignViewBox.ActualHeight > 0 ? page.DesignViewBox.ActualHeight / designHeight : 1.0;

            scaleX *= viewboxScaleX;
            scaleY *= viewboxScaleY;

            double inverseScaleX = 1.0 / scaleX;
            double inverseScaleY = 1.0 / scaleY;

            CenterOverlayScaleTransform.ScaleX = inverseScaleX;
            CenterOverlayScaleTransform.ScaleY = inverseScaleY;
            SizeTextScaleTransform.ScaleX = inverseScaleX;
            SizeTextScaleTransform.ScaleY = inverseScaleY;
            ResizeGripScaleTransform.ScaleX = inverseScaleX;
            ResizeGripScaleTransform.ScaleY = inverseScaleY;
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 240) this.Width = 240;
            if (e.NewSize.Height <= 161) this.Height = 161;
            pxTBox.Text = string.Format("{0:F0} x {1:F0}", e.NewSize.Width, e.NewSize.Height);
            RefreshOverlayScale();
        }   
    }
}
