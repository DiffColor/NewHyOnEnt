using System.Collections.Generic;
using System.Windows;
using System;
using System.Windows.Input;
using System.Linq;
using System.Threading;
using TurtleTools;

namespace AndoW_Manager
{
    public partial class ContentsInfoBatchUpdateWindow : Window
    {      
        private static ContentsInfoBatchUpdateWindow instance = null;

        public static ContentsInfoBatchUpdateWindow Instance
        {
            get
            {
                return instance;
            }
        }

        public ContentsInfoBatchUpdateWindow()
        {
            InitializeComponent();
            instance = this;
            InitEventHandler();
        }

    
        public void InitComboBoxes()
        {
            MinCombo.Items.Clear();
            SecCombo.Items.Clear();
            StartHourCombo.Items.Clear();
            StartMinuteCombo.Items.Clear();
            EndHourCombo.Items.Clear();
            EndMinuteCombo.Items.Clear();

            StartHourCombo.Items.Add(string.Empty);
            StartMinuteCombo.Items.Add(string.Empty);
            EndHourCombo.Items.Add(string.Empty);
            EndMinuteCombo.Items.Add(string.Empty);

            for (int i = 0; i < 60; i++)
            {
                MinCombo.Items.Add(string.Format("{0:D2}", i));
                SecCombo.Items.Add(string.Format("{0:D2}", i));
                StartMinuteCombo.Items.Add(string.Format("{0:D2}", i));
                EndMinuteCombo.Items.Add(string.Format("{0:D2}", i));
            }

            for (int i = 0; i < 24; i++)
            {
                StartHourCombo.Items.Add(string.Format("{0:D2}", i));
                EndHourCombo.Items.Add(string.Format("{0:D2}", i));
            }

            MinCombo.SelectedIndex = 0;
            SecCombo.SelectedIndex = 10;
            StartHourCombo.SelectedIndex = 0;
            StartMinuteCombo.SelectedIndex = 0;
            EndHourCombo.SelectedIndex = 0;
            EndMinuteCombo.SelectedIndex = 0;

            if (PlayTimeChangeCheckBox.IsChecked == true)
            {
                MinCombo.IsEnabled = true;
                SecCombo.IsEnabled = true;
                MinCombo.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
                SecCombo.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
            }
            else
            {
                MinCombo.IsEnabled = false;
                SecCombo.IsEnabled = false;
                MinCombo.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                SecCombo.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        public void InitEventHandler()
        {
            this.Loaded += WindowLoaded;

            DeleteBtn.Click += DeleteBtn_Click;
            SelectAllBtn.Click += BTN0DO_Copy1_Click;   // 전체 선택
            BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;   // 전체 해제

            PreviewKeyDown += ContentsInfoBatchUpdateWindow_KeyDown;

            PlayTimeChangeCheckBox.Checked += PlayTimeChangeCheckBox_ChangeCheck;
            PlayTimeChangeCheckBox.Unchecked += PlayTimeChangeCheckBox_ChangeCheck;

            this.Closing += Window_Closing;
        }

        private void PlayTimeChangeCheckBox_ChangeCheck(object sender, RoutedEventArgs e)
        {
            bool enabled = PlayTimeChangeCheckBox.IsChecked == true;
            MinCombo.IsEnabled = enabled;
            SecCombo.IsEnabled = enabled;
            MinCombo.Foreground = new System.Windows.Media.SolidColorBrush(enabled ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.Gray);
            SecCombo.Foreground = new System.Windows.Media.SolidColorBrush(enabled ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.Gray);
        }

        bool altF4Pressed = false;
        void ContentsInfoBatchUpdateWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == System.Windows.Input.Key.F4)
                altF4Pressed = true;
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (altF4Pressed)
            {
                e.Cancel = true;
                altF4Pressed = false; 
                Hide();
                return;
            }

            instance = null;
        }

        void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            List<ContentsInfoClass> datalist = new List<ContentsInfoClass>();

            foreach (ContentInfoElement item in ContentListBox.Items)
            {
                if (ContentListBox.SelectedItems.Contains(item))
                    continue;

                datalist.Add(item.g_ContentsInfoClass);
            }

            Page1.Instance.UpdateContentsListByEditWindow(datalist);

            SetData();
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)   // 플레이어 모두해제
        {
            ContentListBox.UnselectAll();
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)   // 플레이어 모두선택
        {
            ContentListBox.SelectAll();
        }

        void WindowLoaded(object sender, RoutedEventArgs e)
        {
            InitComboBoxes();
        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            StartDatePicker.Text = string.Empty;
            EndDatePicker.Text = string.Empty;
            StartHourCombo.SelectedIndex = 0;
            StartMinuteCombo.SelectedIndex = 0;
            EndHourCombo.SelectedIndex = 0;
            EndMinuteCombo.SelectedIndex = 0;
            PlayTimeChangeCheckBox.IsChecked = false;
            this.Hide();  
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                SetData();
            }
        }

        private void SetData()
        {
            ContentListBox.Items.Clear();

            int idx = 1;

            foreach (DisplayElementForEditor defe in Page1.Instance.g_DspElmtList)
            {
                if (defe.g_ElementInfoClass.EIF_Name == Page1.Instance.g_CurrentSelectedObjName)
                {
                    foreach (ContentsInfoClass item in defe.g_ElementInfoClass.EIF_ContentsInfoClassList)
                    {
                        ContentInfoElement tmpElement = new ContentInfoElement(item);
                        tmpElement.Width = 270;
                        tmpElement.Height = 27;
                        tmpElement.g_PreventMouse = true;
                        tmpElement.ExitColumn.Width = new GridLength(0, GridUnitType.Star);
                        tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                        tmpElement.Margin = new Thickness(2);
                        tmpElement.EditColumn.Width = new GridLength(0, GridUnitType.Star);
                        tmpElement.ShowPeriodInfo = true;
                        tmpElement.DisplayThisElementInfo();
                        ContentListBox.Items.Add(tmpElement);
                        idx++;
                    }
                    break;
                }
            }

            NoContentsTxt.Visibility = ContentListBox.Items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SecCombo_DropDownOpened(object sender, EventArgs e)
        {
            if (MinCombo.SelectedIndex > 0) return;

            string selectedString = SecCombo.SelectedItem as string;

            SecCombo.Items.Clear();
            for (int i = 5; i < 60; i++)
            {
                SecCombo.Items.Add(string.Format("{0:D2}", i));

            }

            if (int.Parse(selectedString) < 5) selectedString = "05";

            SecCombo.SelectedItem = selectedString;
        }

        private void MinCombo_DropDownClosed(object sender, EventArgs e)
        {
            string selectedString1 = MinCombo.SelectedItem as string;
            string selectedString = SecCombo.SelectedItem as string;

            int startIdx = 0;

            if (int.Parse(selectedString1) < 1) startIdx = 5;

            SecCombo.Items.Clear();
            for (; startIdx < 60; startIdx++)
            {
                SecCombo.Items.Add(string.Format("{0:D2}", startIdx));

            }

            if (int.Parse(selectedString) < 5) selectedString = "05";

            SecCombo.SelectedItem = selectedString;
        }

        private void ClearDateBtn_Click(object sender, RoutedEventArgs e)
        {
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            StartDatePicker.Text = string.Empty;
            EndDatePicker.Text = string.Empty;
            StartHourCombo.SelectedIndex = 0;
            StartMinuteCombo.SelectedIndex = 0;
            EndHourCombo.SelectedIndex = 0;
            EndMinuteCombo.SelectedIndex = 0;
        }

        private void ChangeBtn_Click(object sender, RoutedEventArgs e)
        {
            List<ContentsInfoClass> datalist = new List<ContentsInfoClass>();

            string min = MinCombo.SelectedValue?.ToString() ?? "00";
            string sec = SecCombo.SelectedValue?.ToString() ?? "10";

            string startDate = StartDatePicker.Text;
            string endDate = EndDatePicker.Text;
            string startTime = GetSelectedTime(StartHourCombo, StartMinuteCombo);
            string endTime = GetSelectedTime(EndHourCombo, EndMinuteCombo);
            string validationError = ValidatePeriodInputs();
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                MessageTools.ShowMessageBox(validationError, "확인");
                return;
            }

            if (Page1.Instance != null
                && !Page1.Instance.TryNormalizePeriodDataInputs(
                    startDate,
                    endDate,
                    startTime,
                    endTime,
                    out startDate,
                    out endDate,
                    out startTime,
                    out endTime,
                    out validationError))
            {
                MessageTools.ShowMessageBox(validationError, "확인");
                return;
            }

            bool hasPeriodInput = !string.IsNullOrWhiteSpace(startDate)
                || !string.IsNullOrWhiteSpace(endDate)
                || !string.IsNullOrWhiteSpace(startTime)
                || !string.IsNullOrWhiteSpace(endTime);

            foreach (ContentInfoElement item in ContentListBox.Items)
            {
                if (ContentListBox.SelectedItems.Contains(item))
                {
                    if (PlayTimeChangeCheckBox.IsChecked == true)
                    {
                        item.g_ContentsInfoClass.CIF_PlayMinute = min;
                        item.g_ContentsInfoClass.CIF_PlaySec = sec;
                    }

                    if (hasPeriodInput)
                    {
                        Page1.Instance.SetPeriodData(item.g_ContentsInfoClass, startDate, endDate, startTime, endTime);
                    }
                    item.DisplayThisElementInfo();
                }

                datalist.Add(item.g_ContentsInfoClass);
            }

            Page1.Instance.UpdateContentsListByEditWindow(datalist);
        }

        private string ValidatePeriodInputs()
        {
            bool hasStartHour = string.IsNullOrWhiteSpace(StartHourCombo?.SelectedItem?.ToString()) == false;
            bool hasStartMinute = string.IsNullOrWhiteSpace(StartMinuteCombo?.SelectedItem?.ToString()) == false;
            bool hasEndHour = string.IsNullOrWhiteSpace(EndHourCombo?.SelectedItem?.ToString()) == false;
            bool hasEndMinute = string.IsNullOrWhiteSpace(EndMinuteCombo?.SelectedItem?.ToString()) == false;

            if (hasStartHour != hasStartMinute)
            {
                return "표출 시작 시간은 시와 분을 모두 입력해주세요.";
            }

            if (hasEndHour != hasEndMinute)
            {
                return "표출 종료 시간은 시와 분을 모두 입력해주세요.";
            }

            return string.Empty;
        }

        private void StartDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate == null)
            {
                return;
            }

            if (EndDatePicker.SelectedDate == null && string.IsNullOrWhiteSpace(EndDatePicker.Text))
            {
                EndDatePicker.SelectedDate = StartDatePicker.SelectedDate.Value.Date;
            }
        }

        private void EndDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            return;
        }

        private void StartDate_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StartDatePicker.Text))
            {
                StartDatePicker.SelectedDate = DateTime.Today;
            }
        }

        private void EndDate_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EndDatePicker.Text))
            {
                EndDatePicker.SelectedDate = DateTime.Today;
            }
        }


        ContentInfoElement leadCtrl;
        private void ContentListBox_LayoutUpdated(object sender, EventArgs e)
        {
            if (leadCtrl != null)
            {
                Point pt = leadCtrl.TranslatePoint(new Point(0, 0), ContentListBox);
                if (Point.Equals(org_pt, pt) == false)
                {
                    List<ContentsInfoClass> datalist = new List<ContentsInfoClass>();

                    int idx = 1;
                    foreach (ContentInfoElement cie in ContentListBox.Items)
                    {
                        cie.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                        idx++;

                        datalist.Add(cie.g_ContentsInfoClass);
                    }
                    org_pt = pt;

                    Page1.Instance.UpdateContentsListByEditWindow(datalist);
                }
                leadCtrl = null;
            }
        }

        Point org_pt;
        private void ContentListBox_Drop(object sender, DragEventArgs e)
        {
            leadCtrl = ContentListBox.SelectedItem as ContentInfoElement;

            if (leadCtrl != null)
                org_pt = leadCtrl.TranslatePoint(new Point(0, 0), ContentListBox);
        }

        private void ContentListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int _cnt = ContentListBox.SelectedItems.Count;
            if (_cnt > 0 && _cnt < 2)
            {
                ContentInfoElement _cie = ContentListBox.SelectedItem as ContentInfoElement;
                MinCombo.SelectedItem = _cie.g_ContentsInfoClass.CIF_PlayMinute;
                SecCombo.SelectedItem = _cie.g_ContentsInfoClass.CIF_PlaySec;

                if (_cie == null || _cie.sPD == null)
                {
                    ClearPeriodInputs();
                }
                else
                {
                    UpdatePeriodInputs(_cie.sPD);
                }
            }
        }

        private void UpdatePeriodInputs(PeriodData pd)
        {
            if (pd == null)
            {
                ClearPeriodInputs();
                return;
            }

            if (DateTime.TryParse(pd.StartDate, out var start))
            {
                StartDatePicker.SelectedDate = start;
            }
            else
            {
                StartDatePicker.Text = pd.StartDate ?? string.Empty;
            }

            if (DateTime.TryParse(pd.EndDate, out var end))
            {
                EndDatePicker.SelectedDate = end;
            }
            else
            {
                EndDatePicker.Text = pd.EndDate ?? string.Empty;
            }

            ApplyTimeToInputs(pd.StartTime, StartHourCombo, StartMinuteCombo);
            ApplyTimeToInputs(pd.EndTime, EndHourCombo, EndMinuteCombo);
        }

        private void ClearPeriodInputs()
        {
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            StartDatePicker.Text = string.Empty;
            EndDatePicker.Text = string.Empty;
            StartHourCombo.SelectedIndex = 0;
            StartMinuteCombo.SelectedIndex = 0;
            EndHourCombo.SelectedIndex = 0;
            EndMinuteCombo.SelectedIndex = 0;
        }

        private static void ApplyTimeToInputs(string value, System.Windows.Controls.ComboBox hourCombo, System.Windows.Controls.ComboBox minuteCombo)
        {
            if (hourCombo == null || minuteCombo == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (TimeSpan.TryParse(value, out var parsed))
            {
                hourCombo.SelectedItem = parsed.Hours.ToString("D2");
                minuteCombo.SelectedItem = parsed.Minutes.ToString("D2");
            }
        }

        private static string GetSelectedTime(System.Windows.Controls.ComboBox hourCombo, System.Windows.Controls.ComboBox minuteCombo)
        {
            string hour = hourCombo?.SelectedItem?.ToString();
            string minute = minuteCombo?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(hour) || string.IsNullOrWhiteSpace(minute))
            {
                return string.Empty;
            }

            return string.Format("{0}:{1}", hour.PadLeft(2, '0'), minute.PadLeft(2, '0'));
        }
    }
}
