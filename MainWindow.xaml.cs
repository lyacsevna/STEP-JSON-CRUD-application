using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace STEP_JSON_Application_for_ASKON
{
    public partial class MainWindow : Window
    {
        private string currentFilePath = string.Empty;
        public MainWindow()
        {
            InitializeComponent();
            ViewButton.IsChecked = true;
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
                    currentFilePath = filePath; // Сохраняем путь к текущему файлу
                    MessageBox.Show("Файл успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                if (IsValidJson(filePath))
                {
                    string fileContent = File.ReadAllText(filePath);

                    SelectFileTextBlock.Visibility = Visibility.Collapsed;

                    StepJsonTextBox.Text = fileContent;

                    try
                    {
                        var jsonObject = JObject.Parse(fileContent);
                        var treeNodes = FormatJsonObject(jsonObject);

                        // Очищаем TreeView перед добавлением новых данных
                        TextTabTreeView.Items.Clear();

                        // Добавляем узлы в TreeView
                        foreach (var node in treeNodes)
                        {
                            TextTabTreeView.Items.Add(node);
                        }

                        // Разворачиваем все узлы
                        ExpandAllTreeViewItems(TextTabTreeView);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при десериализации JSON: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
                }
                else
                {
                    MessageBox.Show("Файл не является валидным JSON.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool IsValidJson(string filePath)
        {
            try
            {
                var jsonString = File.ReadAllText(filePath);
                JsonConvert.DeserializeObject(jsonString);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private List<TreeNode> FormatJsonObject(JObject jsonObject)
        {
            var rootNodes = new List<TreeNode>();

            // Группируем объекты по их типам
            var groupedInstances = jsonObject["instances"]
                .GroupBy(instance => instance["type"].ToString())
                .OrderBy(group => group.Key);

            foreach (var group in groupedInstances)
            {
                var typeNode = new TreeNode
                {
                    Name = $"=== {group.Key.ToUpper()} ===",
                    IsExpanded = true // Развернуть узел по умолчанию
                };

                foreach (var instance in group)
                {
                    var instanceNode = new TreeNode
                    {
                        Name = $"ID: {instance["id"]}",
                        IsExpanded = true // Развернуть узел по умолчанию
                    };

                    var attributes = instance["attributes"] as JObject;
                    if (attributes != null)
                    {
                        var attributesNode = new TreeNode
                        {
                            Name = "Attributes",
                            IsExpanded = true // Развернуть узел Attributes по умолчанию
                        };

                        foreach (var property in attributes.Properties().OrderBy(p => p.Name))
                        {
                            attributesNode.Children.Add(new TreeNode
                            {
                                Name = property.Name,
                                Value = property.Value.ToString(),
                                IsExpanded = true // Развернуть дочерние узлы по умолчанию
                            });
                        }

                        instanceNode.Children.Add(attributesNode);
                    }

                    typeNode.Children.Add(instanceNode);
                }

                rootNodes.Add(typeNode);
            }

            return rootNodes;
        }

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

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        // Обработчик события Loaded для TreeView
        private void TextTabTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            // Разворачиваем все узлы при загрузке TreeView
            ExpandAllTreeViewItems(TextTabTreeView);
        }

        // Метод для рекурсивного разворачивания всех узлов TreeView
        private void ExpandAllTreeViewItems(ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsExpanded = true; // Разворачиваем узел
                    if (treeViewItem.Items.Count > 0)
                    {
                        ExpandAllTreeViewItems(treeViewItem); // Рекурсивно разворачиваем дочерние узлы
                    }
                }
            }
        }
    }

    public class TreeNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<TreeNode> Children { get; set; } = new List<TreeNode>();
        public bool IsExpanded { get; set; } // Добавлено свойство для управления состоянием узла
    }
}