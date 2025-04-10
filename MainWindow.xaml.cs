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
                    // Передаем ScrollViewer из SchemaTab
                    schemaManager.GenerateSchema(jsonObject, SchemaCanvas, SchemaTab.Content as ScrollViewer);
                    DefaultFileNameTextBlock.Text = selectedFilePath;
                }
            }
        }

        // GETTING THE PATH OF THE DOWNLOADED FILE IN LISTBOX
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
    }

    public class TreeNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<TreeNode> Children { get; set; } = new List<TreeNode>();
        public bool IsExpanded { get; set; }
    }
}