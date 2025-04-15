using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace STEP_JSON_Application_for_ASKON
{
    public partial class MainWindow : Window
    {
        private string currentFilePath = string.Empty;
        private double scale = 1.0; // Текущий масштаб
        private const double ScaleRate = 0.1; // Шаг изменения масштаба

        private List<string> loadedFilePaths = new List<string>(); //для хранения путей загруженных файлов

        private Stack<string> undoStack = new Stack<string>(); // Для поддержки отмены действий
        private string lastText = string.Empty; // Для хранения последнего текста

        private JsonManager jsonManager = new JsonManager();
        private TreeManager treeManager = new TreeManager();
        private SchemaManager schemaManager = new SchemaManager();

        public MainWindow()
        {
            InitializeComponent();
            // Подключаем обработчик события MouseWheel к SchemaCanvas только в конструкторе
            SchemaCanvas.MouseWheel += SchemaCanvas_MouseWheel;
            // Применяем ScaleTransform к SchemaCanvas
            SchemaCanvas.RenderTransform = new ScaleTransform(scale, scale);
        }

        // Обработчик масштабирования (только для Ctrl, Alt обрабатывается в SchemaManager)
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

                // Ограничиваем масштаб
                scale = Math.Max(0.2, Math.Min(scale, 5.0));

                transform.ScaleX = scale;
                transform.ScaleY = scale;

                // Корректируем размеры холста после масштабирования
                SchemaCanvas.Width = SchemaCanvas.Width * scale / transform.ScaleX + 20;
                SchemaCanvas.Height = SchemaCanvas.Height * scale / transform.ScaleY + 20;

                e.Handled = true; // Предотвращаем дальнейшую обработку события
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath = GetJsonFilePath();

            if (!string.IsNullOrEmpty(filePath))
            {
                ImportJsonFile(filePath);
            }
            else
            {
                MessageBox.Show("Файл не был выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string GetJsonFilePath()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

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
                        // Передаем ScrollViewer из SchemaTab
                        schemaManager.GenerateSchema(jsonObject, SchemaCanvas, SchemaTab.Content as ScrollViewer);
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

            return openFileDialog.FileName;
        }

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
                    schemaManager.GenerateSchema(jsonObject, SchemaCanvas, SchemaTab.Content as ScrollViewer);
                    DefaultFileNameTextBlock.Text = selectedFilePath;
                }
            }
        }

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

        private void ImportJsonFile(string filePath)
        {
            if (loadedFilePaths.Contains(filePath))
            {
                MessageBox.Show("Этот файл уже был загружен.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fileContent;

            try
            {
                fileContent = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при чтении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string processedContent = AddErrorCommentsToJson(fileContent, out string errorDescription);

            SelectFileTextBlock.Visibility = Visibility.Collapsed;
            StepJsonTextBox.Text = processedContent;

            if (!string.IsNullOrEmpty(errorDescription))
            {
                MessageBox.Show(errorDescription, "Ошибка в JSON", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            try
            {
                var jsonObject = JObject.Parse(processedContent);
                var treeNodes = treeManager.FormatJsonObject(jsonObject);

                TextTabTreeView.Items.Clear();
                SchemaCanvas.Children.Clear();

                foreach (var node in treeNodes)
                {
                    TextTabTreeView.Items.Add(node);
                }

                treeManager.ExpandAllTreeViewItems(TextTabTreeView);
                schemaManager.GenerateSchema(jsonObject, SchemaCanvas, SchemaTab.Content as ScrollViewer);
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

            LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
            loadedFilePaths.Add(filePath);
            DefaultFileNameTextBlock.Text = filePath;
            UpdateLoadedFilesList();
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

        private void TestValidButton_Click(object sender, RoutedEventArgs e)
        {
            string jsonContent = StepJsonTextBox.Text;

            // Проверяем синтаксис JSON и получаем возможные ошибки
            string processedContent = AddErrorCommentsToJson(jsonContent, out string errorDescription);

            // Обновляем интерфейс в зависимости от наличия ошибок
            if (!string.IsNullOrEmpty(errorDescription))
            {
                MessageBox.Show(errorDescription, "Ошибка в JSON", MessageBoxButton.OK, MessageBoxImage.Error);
                // Очищаем TreeView и Canvas, так как есть ошибки
                TextTabTreeView.Items.Clear();
                SchemaCanvas.Children.Clear();
            }
            else
            {
                // Если ошибок нет, парсим JSON и обновляем TreeView и Canvas
                try
                {
                    var jsonObject = JObject.Parse(processedContent);
                    var treeNodes = treeManager.FormatJsonObject(jsonObject);

                    // Очищаем предыдущие элементы
                    TextTabTreeView.Items.Clear();
                    SchemaCanvas.Children.Clear();

                    // Добавляем новые элементы в TreeView
                    foreach (var node in treeNodes)
                    {
                        TextTabTreeView.Items.Add(node);
                    }

                    // Раскрываем все элементы дерева
                    treeManager.ExpandAllTreeViewItems(TextTabTreeView);
                    // Генерируем схему
                    schemaManager.GenerateSchema(jsonObject, SchemaCanvas, SchemaTab.Content as ScrollViewer);
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

                    ImportJsonFile(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Произошла ошибка при создании JSON файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // BUTTONS IN THE MENU BAR
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

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        // BUTTONS FOR SWITCHING THE VIEWING AND EDITING MODE
        private void ViewButton_Checked(object sender, RoutedEventArgs e)
        {
            EditorButton.IsChecked = false;
            StepJsonTextBox.IsReadOnly = true;
        }

        private void EditorButton_Checked(object sender, RoutedEventArgs e)
        {
            ViewButton.IsChecked = false;
            StepJsonTextBox.IsReadOnly = false;
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

        private void StepJsonTextBox_TextChanged(object sender, EventArgs e)
        {
            if (StepJsonTextBox.Text != lastText)
            {
                undoStack.Push(lastText);
                lastText = StepJsonTextBox.Text;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }
    }

    public class TreeNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<TreeNode> Children { get; set; } = new List<TreeNode>();
        public bool IsExpanded { get; set; }
    }
}