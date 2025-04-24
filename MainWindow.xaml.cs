using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input; // Добавлено для Keyboard.Modifiers
using System.Windows.Media; // Для ScaleTransform

namespace STEP_JSON_Application_for_ASKON
{
    public partial class MainWindow : Window
    {

        #region Поля

        private string currentFilePath = string.Empty;
        private double scale = 1.0; // Текущий масштаб
        private const double ScaleRate = 0.1; // Шаг изменения масштаба

       
        private static readonly Stack<string> stack = new Stack<string>();
        private Stack<string> undoStack = stack;
        private string lastText = string.Empty;

        private readonly JsonManager jsonManager;
        private static readonly TreeManager treeManager = new TreeManager();
        private static readonly SchemaManager schemaManager = new SchemaManager();

        #endregion

        #region Конструктор
        public MainWindow()
        {
            InitializeComponent();
            undoStack.Push(lastText); //для обработки кнопки возврата изменений

            // Подключаем обработчик события MouseWheel к SchemaCanvas
            SchemaCanvas.MouseWheel += SchemaCanvas_MouseWheel;
            // Применяем ScaleTransform к SchemaCanvas
            SchemaCanvas.RenderTransform = new ScaleTransform(scale, scale);

            jsonManager = new JsonManager(this);
        }

        #endregion


        private void SchemaCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control) // Проверяем, нажат ли Ctrl
            {
                if (!(SchemaCanvas.RenderTransform is ScaleTransform transform)) return;

                // Увеличиваем или уменьшаем масштаб в зависимости от направления прокрутки
                if (e.Delta > 0)
                    scale += ScaleRate; // Увеличение
                else if (e.Delta < 0)
                    scale -= ScaleRate; // Уменьшение

                // Ограничиваем масштаб, чтобы не уйти в отрицательные значения или слишком мелкий/крупный масштаб
                scale = Math.Max(0.2, Math.Min(scale, 5.0));

                transform.ScaleX = scale;
                transform.ScaleY = scale;

                e.Handled = true;
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            jsonManager.SelectJsonFile();
        }

        private void SaveFile(string action)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (action == "Сохранить")
            {
                if (!string.IsNullOrEmpty(currentFilePath))
                {
                    File.WriteAllText(currentFilePath, StepJsonTextBox.Text);
                    MessageBox.Show("Файл успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Сначала используйте 'Сохранить как' для выбора пути.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else if (action == "Сохранить как")
            {
                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;
                    File.WriteAllText(filePath, StepJsonTextBox.Text);
                    currentFilePath = filePath;

                    MessageBox.Show("Файл успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }




        #region Вспомогательные методы
        

        private void TestValidButton_Click(object sender, RoutedEventArgs e)
        {
            string jsonContent = StepJsonTextBox.Text;

            // Проверяем синтаксис JSON и получаем возможные ошибки
            string processedContent = AddErrorCommentsToJson(jsonContent, out string errorDescription);

            // Обновляем интерфейс в зависимости от наличия ошибок
            if (!string.IsNullOrEmpty(errorDescription))
            {
                ErrorJSONTextBox.Text = errorDescription;
                ErrorJSONTextBox.Visibility = Visibility.Visible;
                // Очищаем TreeView и Canvas, так как есть ошибки
                TextTabTreeView.Items.Clear();
                SchemaCanvas.Children.Clear();
            }
            else
            {
                ErrorJSONTextBox.Visibility = Visibility.Collapsed;

                // Если ошибок нет, парсим JSON и обновляем TreeView и Canvas
                try
                {
                    var jsonObject = JObject.Parse(processedContent);
                    var treeNodes = treeManager.FormatJsonObject(jsonObject);

                    // Очищаем предыдущие элементы
                    TextTabTreeView.Items.Clear();
                    SchemaCanvas.Children.Clear();
                    ErrorPanel.Visibility = Visibility.Collapsed;

                    // Добавляем новые элементы в TreeView
                    foreach (var node in treeNodes)
                    {
                        TextTabTreeView.Items.Add(node);
                    }

                    // Раскрываем все элементы дерева
                    treeManager.ExpandAllTreeViewItems(TextTabTreeView);
                    // Генерируем схему
                    schemaManager.GenerateSchema(jsonObject, SchemaCanvas);
                }
                catch (JsonReaderException ex)
                {
                    MessageBox.Show($"Ошибка при десериализации JSON: {ex.Message}\n" +
                                    $"Строка: {ex.LineNumber}, Позиция: {ex.LinePosition}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string AddErrorCommentsToJson(string jsonContent, out string errorDescription) //сделать для множества ошибок
        {
            errorDescription = string.Empty;
            var errors = new List<string>();

            if (string.IsNullOrEmpty(jsonContent))
            {
                errorDescription = "JSON контент не может быть пустым.";
                return jsonContent;
            }
            try
            {
                JToken.Parse(jsonContent);
            }
            catch (JsonReaderException ex)
            {
                errorDescription = $"Ошибка синтаксиса JSON: {ex.Message}\nСтрока: {ex.LineNumber}, Позиция: {ex.LinePosition}";
                return jsonContent;
            }

            using (var reader = new JsonTextReader(new StringReader(jsonContent)))
            {
                while (true)
                {
                    try
                    {
                        if (!reader.Read())
                            break;
                    }
                    catch (JsonReaderException ex)
                    {
                        var errorMessage = $"Ошибка парсинга JSON: {ex.Message}\nСтрока: {ex.LineNumber}, Позиция: {ex.LinePosition}";
                        errors.Add(errorMessage);
                        reader.Skip();
                        continue;
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Необработанная ошибка: {ex.Message}";
                        errors.Add(errorMessage);
                        break;
                    }
                }
            }

            if (errors.Count > 0)
            {
                errorDescription = string.Join("\n", errors);
            }

            return jsonContent;
        }

        //private void LoadedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (LoadedFilesListBox.SelectedItem is string selectedFileName)
        //    {
        //        string selectedFilePath = GetSelectedFilePath(selectedFileName);

        //        if (!string.IsNullOrEmpty(selectedFilePath))
        //        {
        //            string fileContent = File.ReadAllText(selectedFilePath);
        //            StepJsonTextBox.Text = fileContent;

        //            var jsonObject = JObject.Parse(fileContent);
        //            var treeNodes = treeManager.FormatJsonObject(jsonObject);

        //            TextTabTreeView.Items.Clear();
        //            SchemaCanvas.Children.Clear();
        //            foreach (var node in treeNodes)
        //            {
        //                TextTabTreeView.Items.Add(node);
        //            }

        //            treeManager.ExpandAllTreeViewItems(TextTabTreeView);
        //            schemaManager.GenerateSchema(jsonObject, SchemaCanvas);
        //            DefaultFileNameTextBlock.Text = selectedFilePath;
        //        }
        //    }
        //}
        //private string GetSelectedFilePath(string selectedFileName)
        //{
        //    try
        //    {
        //        int index = LoadedFilesListBox.Items.IndexOf(selectedFileName);
        //        if (index >= 0 && index < loadedFilePaths.Count)
        //        {
        //            return loadedFilePaths[index];
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Ошибка при получении пути к файлу: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //    return null;
        //}
        private void StepJsonTextBox_TextChanged(object sender, EventArgs e)
        {
            if (StepJsonTextBox.Text != lastText)
            {
                undoStack.Push(lastText);
                lastText = StepJsonTextBox.Text;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) //РЕАЛИЗОВАТЬ
        {

        }
        #endregion

        #region Вкладка в меню - ФАЙЛ (создать, открыть и т.д)
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

                // Минимальный шаблон JSON
                var data = new
                {
                    format = "ESKD_JSON_V1",
                    schema = "GOST_R_2_525",
                    instances = new List<object>
            {
                new
                {
                    id = "#1",
                    type = "eskd_product",
                    attributes = new
                    {
                        id = "АБВГ.XXXXXX.XXX",
                        name = "Название продукта",
                        description = "Описание продукта",
                        frame_of_reference = "#33",
                        product_type = ".ASSEMBLY."
                    }
                },
                new
                {
                    id = "#2",
                    type = "eskd_product_definition_formation",
                    attributes = new
                    {
                        id = "001",
                        description = "",
                        of_product = "#1",
                        make_or_buy = ".MADE.",
                        standard = ".F."
                    }
                },
                new
                {
                    id = "#3",
                    type = "product_definition",
                    attributes = new
                    {
                        id = "EPS001",
                        description = "Описание для формирования ЭСК по ГОСТ Р 2.525",
                        formation = "#2",
                        frame_of_reference = "#37"
                    }
                },
                new
                {
                    id = "#31",
                    type = "document",
                    attributes = new
                    {
                        id = "АБВГ.XXXXXX.XXXЭМС",
                        name = "Название документа",
                        kind = "#32"
                    }
                },
                new
                {
                    id = "#32",
                    type = "document_type",
                    attributes = new
                    {
                        product_data_type = "ЭМСЕ"
                    }
                },
                new
                {
                    id = "#41",
                    type = "organization",
                    attributes = new
                    {
                        id = "ЕКУЦ",
                        name = "АО «Организация002»"
                    }
                },
                new
                {
                    id = "#42",
                    type = "eskd_organization_product_assignment",
                    attributes = new
                    {
                        assigned_product = "#1",
                        assigned_organization = "#41",
                        role = "#43"
                    }
                },
                new
                {
                    id = "#43",
                    type = "organization_role",
                    attributes = new
                    {
                        name = "Разработчик"
                    }
                }
            }
                };

                try
                {
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                    MessageBox.Show("JSON файл успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    //jsonManager.SelectJsonFile(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Произошла ошибка при создании JSON файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            ImportButton_Click(sender, e);
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFile("Сохранить");
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFile("Сохранить как");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region Вкладка в меню - ПРАВКА (редактирование файла)
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
            StepJsonTextBox?.Copy();
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            StepJsonTextBox?.Paste();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            StepJsonTextBox?.Undo();
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 1)
            {
                StepJsonTextBox.TextChanged -= StepJsonTextBox_TextChanged;

                undoStack.Pop();
                StepJsonTextBox.Text = undoStack.Peek();

                lastText = StepJsonTextBox.Text;

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
            StepJsonTextBox?.SelectAll();
        }

        #endregion

        #region  Вкладка в меню - СПРАВКА 
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