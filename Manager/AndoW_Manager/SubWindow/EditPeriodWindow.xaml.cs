using System;
using System.Windows;
using System.Windows.Input;

namespace AndoW_Manager
{
    /// <summary>
    /// EditPeriodWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EditPeriodWindow : Window
    {
        public ContentsInfoClass TargetContent { get; private set; }
        private bool _periodCleared;

        public EditPeriodWindow(ContentsInfoClass content)
        {
            InitializeComponent();
            TargetContent = content ?? new ContentsInfoClass();
            InitCombo();
            Loaded += EditPeriodWindow_Loaded;
        }

        private void InitCombo()
        {
            MinCombo.Items.Clear();
            SecCombo.Items.Clear();

            for (int i = 0; i < 60; i++)
            {
                string value = string.Format("{0:D2}", i);
                MinCombo.Items.Add(value);
                SecCombo.Items.Add(value);
            }

            MinCombo.SelectedIndex = 0;
            SecCombo.SelectedIndex = 10;
        }

        private void EditPeriodWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (TargetContent == null)
            {
                return;
            }

            MinCombo.SelectedItem = TargetContent.CIF_PlayMinute.PadLeft(2, '0');
            SecCombo.SelectedItem = TargetContent.CIF_PlaySec.PadLeft(2, '0');

            var period = Page1.Instance?.GetPeriodData(TargetContent.CIF_StrGUID);
            if (period != null)
            {
                if (DateTime.TryParse(period.StartDate, out var start))
                {
                    StartDatePicker.SelectedDate = start;
                }
                else if (!string.IsNullOrWhiteSpace(period.StartDate))
                {
                    StartDatePicker.Text = period.StartDate;
                }

                if (DateTime.TryParse(period.EndDate, out var end))
                {
                    EndDatePicker.SelectedDate = end;
                }
                else if (!string.IsNullOrWhiteSpace(period.EndDate))
                {
                    EndDatePicker.Text = period.EndDate;
                }
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TargetContent == null)
            {
                DialogResult = false;
                Close();
                return;
            }

            TargetContent.CIF_PlayMinute = MinCombo.SelectedItem?.ToString() ?? "00";
            TargetContent.CIF_PlaySec = SecCombo.SelectedItem?.ToString() ?? "10";

            string startDate = StartDatePicker.Text;
            string endDate = EndDatePicker.Text;

            bool hasStart = string.IsNullOrWhiteSpace(startDate) == false;
            bool hasEnd = string.IsNullOrWhiteSpace(endDate) == false;

            if (_periodCleared && !hasStart && !hasEnd)
            {
                Page1.Instance?.DeletePeriodData(TargetContent);
            }
            else
            {
                Page1.Instance?.SetPeriodData(TargetContent, startDate, endDate);
            }

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnWin_close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ClearDateBtn_Click(object sender, RoutedEventArgs e)
        {
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            StartDatePicker.Text = string.Empty;
            EndDatePicker.Text = string.Empty;

            _periodCleared = true;
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void StartDatePicker_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StartDatePicker.Text))
            {
                StartDatePicker.SelectedDate = DateTime.Today;
            }
        }

        private void EndDatePicker_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EndDatePicker.Text))
            {
                EndDatePicker.SelectedDate = DateTime.Today;
            }
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
                return;
            }

            if (EndDatePicker.SelectedDate == null)
            {
                return;
            }

            DateTime start = StartDatePicker.SelectedDate.Value.Date;
            DateTime end = EndDatePicker.SelectedDate.Value.Date;
            if (start > end)
            {
                EndDatePicker.SelectedDate = start;
            }
        }

        private void EndDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
            {
                return;
            }

            DateTime start = StartDatePicker.SelectedDate.Value.Date;
            DateTime end = EndDatePicker.SelectedDate.Value.Date;
            if (end < start)
            {
                StartDatePicker.SelectedDate = end;
            }
        }
    }
}
