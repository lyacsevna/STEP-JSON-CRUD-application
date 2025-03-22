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
        public MainWindow()
        {
            InitializeComponent();
            ViewButton.IsChecked = true;

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

                        
                        TextTabTreeView.Items.Clear();

                        
                        foreach (var node in treeNodes)
                        {
                            TextTabTreeView.Items.Add(node);
                        }

                        
                        ExpandAllTreeViewItems(TextTabTreeView);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при десериализации JSON: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    LoadedFilesListBox.Items.Add(filePath);
                }
                else
                {
                    MessageBox.Show("Файл не является валидным JSON.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadedFilesListBox.Items.Count < 2)
            {
                MessageBox.Show("Пожалуйста, загрузите два файла для сравнения.");
                return;
            }

            string file1Path = LoadedFilesListBox.Items[0].ToString();
            string file2Path = LoadedFilesListBox.Items[1].ToString();

            string file1Content = File.ReadAllText(file1Path);
            string file2Content = File.ReadAllText(file2Path);

         
            string fileName1 = System.IO.Path.GetFileName(file1Path);
            string fileName2 = System.IO.Path.GetFileName(file2Path);

            
            JsonComparisonWindow comparisonWindow = new JsonComparisonWindow(file1Content, file2Content, fileName1, fileName2);
            comparisonWindow.ShowDialog();
        }

        // JSON Validation
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



        // Work with TabItem TEXT (TREE VIEV ITEMS)


        private List<TreeNode> FormatJsonObject(JObject jsonObject)
        {
            var rootNodes = new List<TreeNode>();

            var groupedInstances = jsonObject["instances"]
                .GroupBy(instance => instance["type"].ToString())
                .OrderBy(group => group.Key);

            foreach (var group in groupedInstances)
            {
                var typeNode = new TreeNode
                {
                    Name = $"=== {group.Key.ToUpper()} ===",
                    IsExpanded = true
                };

                foreach (var instance in group)
                {
                    var instanceNode = new TreeNode
                    {
                        Name = $"ID: {instance["id"]}",
                        IsExpanded = true
                    };

                    var attributes = instance["attributes"] as JObject;
                    if (attributes != null)
                    {
                        var attributesNode = new TreeNode
                        {
                            Name = "Attributes",
                            IsExpanded = true
                        };

                        foreach (var property in attributes.Properties().OrderBy(p => p.Name))
                        {
                            attributesNode.Children.Add(new TreeNode
                            {
                                Name = property.Name,
                                Value = property.Value.ToString(),
                                IsExpanded = true
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

        private void TextTabTreeView_Loaded(object sender, RoutedEventArgs e)
        {

            ExpandAllTreeViewItems(TextTabTreeView);
        }

        private void ExpandAllTreeViewItems(ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsExpanded = true; 
                    if (treeViewItem.Items.Count > 0)
                    {
                        ExpandAllTreeViewItems(treeViewItem); 
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
        public bool IsExpanded { get; set; }
    }
}