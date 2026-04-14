using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace STEP_JSON_Application_for_ASKON
{
    public partial class MainWindow : Window
    {
        #region Поля
        private double scale = 1.0;
        private const double ScaleRate = 0.1;
        private static readonly Stack<string> stack = new Stack<string>();
        private readonly Stack<string> undoStack = stack;
        private string lastText = string.Empty;
        private readonly JsonManager jsonManager;
        private readonly TreeManager treeManager;
        private readonly SchemaManager schemaManager;
        public SchemaManager SchemaManager => schemaManager;
        #endregion

        #region Конструктор
        public MainWindow()
        {
            InitializeComponent();
            undoStack.Push(lastText);
            SchemaCanvas.MouseWheel += SchemaCanvas_MouseWheel;
            SchemaCanvas.RenderTransform = new ScaleTransform(scale, scale);

            jsonManager = new JsonManager(this);
            treeManager = new TreeManager();
            schemaManager = new SchemaManager(jsonManager);
            StepJsonTextBox.TextChanged += StepJsonTextBox_TextChanged;
            StepJsonTextBox.TextChanged += UpdateUndoStackOnTextChange;
        }
        #endregion

        private void SchemaCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (!(SchemaCanvas.RenderTransform is ScaleTransform transform)) return;

                if (e.Delta > 0)
                    scale += ScaleRate;
                else if (e.Delta < 0)
                    scale -= ScaleRate;

                scale = Math.Max(0.2, Math.Min(scale, 5.0));

                transform.ScaleX = scale;
                transform.ScaleY = scale;
                e.Handled = true;
            }
        }

        private void StepJsonTextBox_TextChanged(object sender, EventArgs e)
        {
            string fileContent = StepJsonTextBox.Text;
            string filePath = DefaultFileNameTextBlock.Text;
            if (!string.IsNullOrEmpty(fileContent))
            {
                jsonManager.TestValidCurrentFileContent(fileContent, filePath);
            }
        }

        private void UpdateUndoStackOnTextChange(object sender, EventArgs e)
        {
            if (StepJsonTextBox.Text != lastText)
            {
                undoStack.Push(lastText);
                lastText = StepJsonTextBox.Text;
            }
        }

        private void LoadedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            jsonManager.DisplaySelectedFileContent(sender, e);
        }

        #region Вкладка в меню - ФАЙЛ
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            jsonManager.ImportJsonFile();
        }

        private void CreateNewFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                Title = "Создать новый JSON файл"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;

                var data = new
                {
                    format = "ESKD_JSON_V1",
                    schema = "GOST_R_2_525",
                    instances = new List<object>
                    {
                        new { id = "#1", type = "eskd_product", attributes = new { id = "АБВГ.XXXXXX.XXX", name = "Название продукта", description = "Описание продукта", frame_of_reference = "#33", product_type = ".ASSEMBLY." } },
                        new { id = "#2", type = "eskd_product_definition_formation", attributes = new { id = "001", description = "", of_product = "#1", make_or_buy = ".MADE.", standard = ".F." } },
                        new { id = "#3", type = "product_definition", attributes = new { id = "EPS001", description = "Описание для формирования ЭСК по ГОСТ Р 2.525", formation = "#2", frame_of_reference = "#37" } },
                        new { id = "#31", type = "document", attributes = new { id = "АБВГ.XXXXXX.XXXЭМС", name = "Название документа", kind = "#32" } },
                        new { id = "#32", type = "document_type", attributes = new { product_data_type = "ЭМСЕ" } },
                        new { id = "#41", type = "organization", attributes = new { id = "ЕКУЦ", name = "АО «Организация002»" } },
                        new { id = "#42", type = "eskd_organization_product_assignment", attributes = new { assigned_product = "#1", assigned_organization = "#41", role = "#43" } },
                        new { id = "#43", type = "organization_role", attributes = new { name = "Разработчик" } }
                    }
                };

                try
                {
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                    MessageBox.Show("JSON файл успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Произошла ошибка при создании JSON файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            jsonManager.SaveFile("Сохранить");
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            jsonManager.SaveFile("Сохранить как");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion

        #region Вкладка в меню - ПРАВКА
        private void CutButton_Click(object sender, RoutedEventArgs e)
        {
            if (StepJsonTextBox != null)
            {
                StepJsonTextBox.Copy();
                StepJsonTextBox.SelectedText = "";
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (StepJsonTextBox != null)
            {
                StepJsonTextBox.Copy();
            }
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (StepJsonTextBox != null)
            {
                StepJsonTextBox.Paste();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (StepJsonTextBox != null)
            {
                StepJsonTextBox.Undo();
            }
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 1)
            {
                StepJsonTextBox.TextChanged -= StepJsonTextBox_TextChanged;
                StepJsonTextBox.TextChanged -= UpdateUndoStackOnTextChange;

                undoStack.Pop();
                StepJsonTextBox.Text = undoStack.Peek();
                lastText = StepJsonTextBox.Text;

                StepJsonTextBox.TextChanged += UpdateUndoStackOnTextChange;
                StepJsonTextBox.TextChanged += StepJsonTextBox_TextChanged;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (StepJsonTextBox != null)
            {
                StepJsonTextBox.SelectedText = "";
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (StepJsonTextBox != null)
            {
                StepJsonTextBox.SelectAll();
            }
        }
        #endregion

        #region Вкладка в меню - СПРАВКА
        private void InformationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }
        #endregion
    }

    #region Для дерева
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}