using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Добавлено для Keyboard.Modifiers
using System.Windows.Media; // Для ScaleTransform

namespace STEP_JSON_Application_for_ASKON
{
    public partial class MainWindow : Window
    {
        private string currentFilePath = string.Empty;
        private double scale = 1.0; // Текущий масштаб
        private const double ScaleRate = 0.1; // Шаг изменения масштаба


        private List<string> loadedFilePaths = new List<string>();
        private Stack<string> undoStack = new Stack<string>();
        private string lastText = string.Empty;


        private JsonManager jsonManager = new JsonManager();
        private TreeManager treeManager = new TreeManager();
        private SchemaManager schemaManager = new SchemaManager();

        public MainWindow()
        {
            InitializeComponent();
            undoStack.Push(lastText); //для обработки кнопки возврата изменений

            // Подключаем обработчик события MouseWheel к SchemaCanvas
            SchemaCanvas.MouseWheel += SchemaCanvas_MouseWheel;
            // Применяем ScaleTransform к SchemaCanvas
            SchemaCanvas.RenderTransform = new ScaleTransform(scale, scale);
        }

        // Обработчик масштабирования
        //============================================================================================================
        private void SchemaCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control) // Проверяем, нажат ли Ctrl
            {
                var transform = SchemaCanvas.RenderTransform as ScaleTransform;
                if (transform == null) return;

                // Увеличиваем или уменьшаем масштаб в зависимости от направления прокрутки
                if (e.Delta > 0)
                    scale += ScaleRate; // Увеличение
                else if (e.Delta < 0)
                    scale -= ScaleRate; // Уменьшение

                // Ограничиваем масштаб, чтобы не уйти в отрицательные значения или слишком мелкий/крупный масштаб
                scale = Math.Max(0.2, Math.Min(scale, 5.0));

                transform.ScaleX = scale;
                transform.ScaleY = scale;

                e.Handled = true; // Предотвращаем дальнейшую обработку события
            }
        }

        //============================================================================================================

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string fileContent;

                try
                {
                    fileContent = File.ReadAllText(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при чтении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return; // Завершаем выполнение метода, если не удалось прочитать файл
                }

                // Обработка ошибок и добавление комментариев
                string errorDescription;
                string processedContent = AddErrorCommentsToJson(fileContent, out errorDescription);

                // Обновляем текстовое поле с содержимым файла
                SelectFileTextBlock.Visibility = Visibility.Collapsed;
                StepJsonTextBox.Text = processedContent;

                // Проверяем, есть ли ошибки и обновляем ErrorJSONTextBox
                if (!string.IsNullOrEmpty(errorDescription))
                {
                    ErrorJSONTextBox.Text = errorDescription;
                    ErrorJSONTextBox.Visibility = Visibility.Visible; // Показываем текст ошибки
                }
                else
                {
                    ErrorJSONTextBox.Visibility = Visibility.Collapsed; // Скрываем текст ошибки, если нет
                }

                try
                {
                    var jsonObject = JObject.Parse(processedContent); // Пытаемся парсить обработанный контент
                    var treeNodes = treeManager.FormatJsonObject(jsonObject);

                    TextTabTreeView.Items.Clear();
                    SchemaCanvas.Children.Clear();

                    foreach (var node in treeNodes)
                    {
                        TextTabTreeView.Items.Add(node);
                    }

                    treeManager.ExpandAllTreeViewItems(TextTabTreeView);
                    schemaManager.GenerateSchema(jsonObject, SchemaCanvas);
                }
                catch (JsonReaderException ex)
                {
                    MessageBox.Show($"Ошибка при десериализации JSON: {ex.Message}\n" +
                                    $"Строка: {ex.LineNumber}, Позиция: {ex.LinePosition}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    // Обработка других исключений
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
                loadedFilePaths.Add(filePath);
                DefaultFileNameTextBlock.Text = filePath;
                UpdateLoadedFilesList();
            }
        }

        private string AddErrorCommentsToJson(string jsonContent, out string errorDescription)
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
                errorDescription = $"Ошибка в JSON: {ex.Message}\nСтрока: {ex.LineNumber}, Позиция: {ex.LinePosition}";
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
                        // Обработка других возможных исключений
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






        // UPDATE LISTBOX WHEN IMPORTING
        private void UpdateLoadedFilesList()
        {
            if (LoadedFilesListBox.Items.Count == 0)
            {
                EmptyFilesMessage.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyFilesMessage.Visibility = Visibility.Collapsed;
                LoadedFilesListBox.Visibility = Visibility.Visible;
            }
        }


        // SELECTING FILES FROM UPLOADED TO LISTBOX
        private void LoadedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LoadedFilesListBox.SelectedItem is string selectedFileName)
            {
                string selectedFilePath = GetSelectedFilePath(selectedFileName);

                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    string fileContent = File.ReadAllText(selectedFilePath);
                    StepJsonTextBox.Text = fileContent;

                    var jsonObject = JObject.Parse(fileContent);
                    var treeNodes = treeManager.FormatJsonObject(jsonObject);

                    
                    TextTabTreeView.Items.Clear();
                    SchemaCanvas.Children.Clear();
                    foreach (var node in treeNodes)
                    {
                        TextTabTreeView.Items.Add(node);
                    }

                    treeManager.ExpandAllTreeViewItems(TextTabTreeView);
                    schemaManager.GenerateSchema(jsonObject, SchemaCanvas);
                    DefaultFileNameTextBlock.Text = selectedFilePath;

                }
            }
        }

        private void StepJsonTextBox_TextChanged(object sender, EventArgs e)
        {

            if (StepJsonTextBox.Text != lastText)
            {
                undoStack.Push(lastText);
                lastText = StepJsonTextBox.Text;
            }
        }

        // GETTING THE PATH OF THE DOWNLOADED FILE IN LITBOX
        private string GetSelectedFilePath(string selectedFileName)
        {
            try
            {
                int index = LoadedFilesListBox.Items.IndexOf(selectedFileName);
                if (index >= 0 && index < loadedFilePaths.Count)
                {
                    return loadedFilePaths[index];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении пути к файлу: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        // SAVING A FILE WHEN EDITING
        private void SaveFile(string action)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

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


        // ПОИСК (НЕ РЕАЛИЗОВАН)
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }




        // ВКЛАДКА В МЕНЮ = ФАЙЛ
        //============================================================================================================
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
                    Name = "Пример",
                    DateCreated = DateTime.Now,
                    Content = "Это пример содержимого JSON файла."
                };

                try
                {
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                    MessageBox.Show("JSON файл успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    ImportJsonFile(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Произошла ошибка при создании JSON файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportJsonFile(string filePath)
        {
            if (jsonManager.IsValidJson(filePath))
            {
                string fileContent = File.ReadAllText(filePath);
                SelectFileTextBlock.Visibility = Visibility.Collapsed;
                StepJsonTextBox.Text = fileContent;

                try
                {
                    var jsonObject = JObject.Parse(fileContent);
                    var treeNodes = treeManager.FormatJsonObject(jsonObject);

                    TextTabTreeView.Items.Clear();
                    SchemaCanvas.Children.Clear();

                    foreach (var node in treeNodes)
                    {
                        TextTabTreeView.Items.Add(node);
                    }

                    treeManager.ExpandAllTreeViewItems(TextTabTreeView);
                    schemaManager.GenerateSchema(jsonObject, SchemaCanvas);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при десериализации JSON: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
                loadedFilePaths.Add(filePath);
                DefaultFileNameTextBlock.Text = filePath;
                UpdateLoadedFilesList();
            }
            else
            {
                MessageBox.Show("Файл не является валидным JSON.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
        //============================================================================================================





        // ВКЛАДКА В МЕНЮ = ПРАВКА
        //============================================================================================================
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
            if (StepJsonTextBox != null)
            {
                StepJsonTextBox.SelectAll();
            }
        }

        //============================================================================================================

        // ВКЛАДКА В МЕНЮ = CПРАВКА
        private void InformationMenuItem_Click(object sender, RoutedEventArgs e)
        {
           
        }

        //============================================================================================================
    }

    // SOMETHING FOR WORKING WITH WOOD.....
    public class TreeNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<TreeNode> Children { get; set; } = new List<TreeNode>();
        public bool IsExpanded { get; set; }
    }
}






















//                    _oo0oo_
//                   o8888888o
//                   88" . "88
//                   (| -_- |)
//                   0\  =  /0
//                 ___/`---'\___
//               .' \\|     |// '.
//              / \\|||  :  |||// \
//             / _||||| -:- |||||- \
//            |   | \\\  -  /// |   |
//            | \_|  ''\---/''  |_/ |
//            \  .-\__  '-'  ___/-. /
//          ___'. .'  /--.--\  `. .'___
//       ."" '<  `.___\_<|>_/___.' >' "".
//      | | :  `- \`.;`\ _ /`;.`/ - ` : | |
//      \  \ `_.   \_ __\ /__ _/   .-` /  /
//  =====`-.____`.___ \_____/___.-`___.-'=====
//                       `=---='
//
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//
//            God Bless         No Bugs