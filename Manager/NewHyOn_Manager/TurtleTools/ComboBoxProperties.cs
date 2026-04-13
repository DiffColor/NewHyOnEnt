using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;

namespace TurtleTools
{
    public class EnumBindingSourceExtension : MarkupExtension
    {
        private Type _enumType;
        public Type EnumType
        {
            get { return this._enumType; }
            set
            {
                if (value != this._enumType)
                {
                    if (null != value)
                    {
                        Type enumType = Nullable.GetUnderlyingType(value) ?? value;
                        if (!enumType.IsEnum)
                            throw new ArgumentException("Type must be for an Enum.");
                    }

                    this._enumType = value;
                }
            }
        }

        public EnumBindingSourceExtension() { }

        public EnumBindingSourceExtension(Type enumType)
        {
            this.EnumType = enumType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (null == this._enumType)
                throw new InvalidOperationException("The EnumType must be specified.");

            Type actualEnumType = Nullable.GetUnderlyingType(this._enumType) ?? this._enumType;
            Array enumValues = Enum.GetValues(actualEnumType);

            if (actualEnumType == this._enumType)
                return enumValues;

            Array tempArray = Array.CreateInstance(actualEnumType, enumValues.Length + 1);
            enumValues.CopyTo(tempArray, 1);
            return tempArray;
        }
    }

    public class EnumDescriptionTypeConverter : EnumConverter
    {
        public EnumDescriptionTypeConverter(Type type)
            : base(type)
        {
        }
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                if (value != null)
                {
                    FieldInfo fi = value.GetType().GetField(value.ToString());
                    if (fi != null)
                    {
                        var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                        return ((attributes.Length > 0) && (!String.IsNullOrEmpty(attributes[0].Description))) ? attributes[0].Description : value.ToString();
                    }
                }

                return string.Empty;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public static class EnumHelper<T>
    {
        public static string GetEnumDescription(string value)
        {
            Type type = typeof(T);
            var name = Enum.GetNames(type).Where(f => f.Equals(value, StringComparison.CurrentCultureIgnoreCase)).Select(d => d).FirstOrDefault();

            if (name == null)
            {
                return string.Empty;
            }
            var field = type.GetField(name);
            var customAttribute = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return customAttribute.Length > 0 ? ((DescriptionAttribute)customAttribute[0]).Description : name;
        }

        public static T GetValueByDescription(string desc)
        {
            Array enumValues = Enum.GetValues(typeof(T));

            foreach (object value in enumValues)
            {
                if (desc == GetEnumDescription((Enum)value))
                    return (T)value;
            }

            throw new ArgumentException("No matching description value found.");
        }

        public static string GetEnumDescription(Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            var attributes = fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes.Length > 0)
                return ((DescriptionAttribute)attributes[0]).Description;
            else
                return value.ToString();
        }

        public static T GetEnumFromDescription(string stringValue)
        {
            foreach (object e in Enum.GetValues(typeof(T)))
                if (GetEnumDescription((Enum)e).Equals(stringValue))
                    return (T)e;
            throw new ArgumentException("No matching enum value found.");
        }

        public static IEnumerable<string> GetEnumDescriptions()
        {
            Type enumType = typeof(T);
            var strings = new Collection<string>();
            foreach (Enum e in Enum.GetValues(enumType))
                strings.Add(GetEnumDescription(e));
            return strings;
        }

        public static IList ToList()
        {
            Type enumType = typeof(T);
            ArrayList list = new ArrayList();
            Array enumValues = Enum.GetValues(enumType);

            foreach (Enum value in enumValues)
            {
                list.Add(new KeyValuePair<string, Enum>(GetEnumDescription(value), value));
            }

            return list;
        }
    }

    public static class ComboBoxDropDownBehavior
    {
        private const double DefaultDropDownMaxHeight = 320d;
        private const double MinimumVisibleDropDownHeight = 120d;
        private const double DropDownPadding = 8d;
        private const double ApproximateItemHeight = 28d;

        public static void ApplyToWindow(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.Loaded += (_, __) => AttachToDescendants(window);
            window.ContentRendered += (_, __) => AttachToDescendants(window);
        }

        public static void ApplyToControl(FrameworkElement control)
        {
            if (control == null)
            {
                return;
            }

            control.Loaded += (_, __) => AttachToDescendants(control);
        }

        private static void AttachToDescendants(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (ComboBox comboBox in FindVisualChildren<ComboBox>(root))
            {
                comboBox.DropDownOpened -= ComboBox_DropDownOpened;
                comboBox.DropDownOpened += ComboBox_DropDownOpened;
            }
        }

        private static void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (!(sender is ComboBox comboBox))
            {
                return;
            }

            comboBox.Dispatcher.BeginInvoke(new Action(() => AdjustDropDown(comboBox)), DispatcherPriority.Loaded);
        }

        private static void AdjustDropDown(ComboBox comboBox)
        {
            Window ownerWindow = Window.GetWindow(comboBox);
            if (ownerWindow == null || comboBox.ActualHeight <= 0)
            {
                return;
            }

            Popup popup = comboBox.Template?.FindName("PART_Popup", comboBox) as Popup;
            if (popup == null)
            {
                return;
            }

            Point comboTopLeft = comboBox.TransformToAncestor(ownerWindow).Transform(new Point(0, 0));
            double availableBelow = ownerWindow.ActualHeight - comboTopLeft.Y - comboBox.ActualHeight - DropDownPadding;
            double availableAbove = comboTopLeft.Y - DropDownPadding;
            double desiredHeight = GetDesiredDropDownHeight(comboBox);

            bool openUpward = availableBelow < Math.Min(desiredHeight, MinimumVisibleDropDownHeight) &&
                              availableAbove > availableBelow;

            double availableHeight = openUpward ? availableAbove : availableBelow;
            double boundedHeight = Math.Max(comboBox.ActualHeight, Math.Min(desiredHeight, Math.Max(availableHeight, comboBox.ActualHeight)));

            comboBox.MaxDropDownHeight = boundedHeight;
            popup.PlacementTarget = comboBox;
            popup.Placement = openUpward ? PlacementMode.Top : PlacementMode.Bottom;
            popup.VerticalOffset = 0;
        }

        private static double GetDesiredDropDownHeight(ComboBox comboBox)
        {
            if (comboBox.Items.Count <= 0)
            {
                return comboBox.ActualHeight;
            }

            double itemHeight = ApproximateItemHeight;
            if (comboBox.ItemContainerStyle != null)
            {
                object configuredHeight = comboBox.ItemContainerStyle.Setters
                    .OfType<Setter>()
                    .FirstOrDefault(setter => setter.Property == FrameworkElement.HeightProperty)?.Value;

                if (configuredHeight is double styleHeight && styleHeight > 0)
                {
                    itemHeight = styleHeight;
                }
            }

            return Math.Min(DefaultDropDownMaxHeight, comboBox.Items.Count * itemHeight + DropDownPadding);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                yield break;
            }

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
