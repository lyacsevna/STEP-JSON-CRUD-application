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
    public class JsonManager
    {
        public List<string> loadedFilePaths = new List<string>();
        private static readonly TreeManager treeManager = new TreeManager();
        private MainWindow mainWindow;
        private string currentFilePath;

        public JsonManager(MainWindow mainWindow)
        {
            if (mainWindow == null)
                throw new ArgumentNullException("mainWindow");
            this.mainWindow = mainWindow;
            Console.WriteLine("JsonManager initialized");
        }

        public void ImportJsonFile()
        {
            var selectedFilePath = OpenFileDialogForJson();
            if (selectedFilePath == null) return;

            if (loadedFilePaths.Contains(selectedFilePath))
            {
                MessageBox.Show("Этот файл уже был загружен.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fileContent = ReadFileContent(selectedFilePath);
            if (fileContent == null) return;

            ProcessJsonFile(fileContent, selectedFilePath);
        }

        private string OpenFileDialogForJson()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() != true)
            {
                MessageBox.Show("Файл не был выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return openFileDialog.FileName;
        }

        private string ReadFileContent(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при чтении файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public void ProcessJsonFile(string fileContent, string selectedFilePath)
        {
            Console.WriteLine($"ProcessJsonFile: filePath={selectedFilePath}, contentLength={fileContent?.Length}");
            string errorDescription;
            bool isValidJson = TryValidateJsonSyntax(fileContent, out errorDescription);

            string processedContent = fileContent;

            UpdateInterface(processedContent, isValidJson ? string.Empty : errorDescription, selectedFilePath);

            if (isValidJson && TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                ClearPreviousData();
                FillTreeViewWithJsonNodes(jsonObject);
                GenerateSchema(jsonObject);
                mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
                currentFilePath = selectedFilePath;
            }
            else
            {
                ShowError(errorDescription);
            }
        }

        public void TestValidCurrentFileContent(string fileContent, string filePath)
        {
            Console.WriteLine($"TestValidCurrentFileContent: filePath={filePath}, contentLength={fileContent?.Length}");
            string errorDescription;
            bool isValidJson = TryValidateJsonSyntax(fileContent, out errorDescription);

            string processedContent = fileContent;
            ClearPreviousData();
            if (isValidJson && TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                UpdateTreeViewFromJson(processedContent);
                GenerateSchema(jsonObject);
                mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
                currentFilePath = filePath;
            }
            else
            {
                ShowError(errorDescription);
            }
        }

        private bool TryConvertJsonToJObject(string processedContent, out JObject jsonObject)
        {
            try
            {
                jsonObject = JObject.Parse(processedContent);
                return true;
            }
            catch (JsonReaderException ex)
            {
                MessageBox.Show("Ошибка при десериализации JSON: " + ex.Message + "\n" +
                                "Строка: " + ex.LineNumber + ", Позиция: " + ex.LinePosition, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                jsonObject = null;
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                jsonObject = null;
                return false;
            }
        }

        private bool TryValidateJsonSyntax(string jsonContent, out string errorDescription)
        {
            errorDescription = string.Empty;

            if (string.IsNullOrEmpty(jsonContent))
            {
                errorDescription = "JSON контент не может быть пустым.";
                return false;
            }

            try
            {
                JToken.Parse(jsonContent);
                return true;
            }
            catch (JsonReaderException ex)
            {
                errorDescription = "Ошибка синтаксиса JSON: " + ex.Message + "\nСтрока: " + ex.LineNumber + ", Позиция: " + ex.LinePosition;
                return false;
            }
        }

        private void ShowError(string errorDescription)
        {
            mainWindow.ErrorJSONTextBox.Text = errorDescription;
            mainWindow.ErrorPanel.Visibility = Visibility.Visible;
        }

        private void UpdateInterface(string processedContent, string errorDescription, string filePath)
        {
            if (loadedFilePaths.Contains(filePath))
            {
                UpdateTreeViewFromJson(processedContent);
            }
            else
            {
                UpdateUIAfterFileLoad(processedContent, errorDescription, filePath);
            }
        }

        private void UpdateUIAfterFileLoad(string processedContent, string errorDescription, string filePath)
        {
            UpdateLoadedFilesList(filePath);
            ClearPreviousData();
            mainWindow.SelectFileTextBlock.Visibility = Visibility.Collapsed;
            mainWindow.StepJsonTextBox.Text = processedContent;

            if (string.IsNullOrEmpty(errorDescription))
            {
                UpdateTreeViewFromJson(processedContent);
            }
            else
            {
                ShowError(errorDescription);
            }

            treeManager.ExpandAllTreeViewItems(mainWindow.TextTabTreeView);
        }

        private void UpdateTreeViewFromJson(string processedContent)
        {
            if (!TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                ShowError("Ошибка при преобразовании JSON в объект.");
                return;
            }

            ClearPreviousData();
            FillTreeViewWithJsonNodes(jsonObject);
            GenerateSchema(jsonObject);
            mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
        }

        private void ClearPreviousData()
        {
            mainWindow.TextTabTreeView.Items.Clear();
            mainWindow.SchemaCanvas.Children.Clear();
        }

        private void FillTreeViewWithJsonNodes(JObject jsonObject)
        {
            var newNodes = treeManager.FormatJsonObject(jsonObject);
            UpdateTreeNodes(mainWindow.TextTabTreeView, newNodes);
        }

        private void UpdateTreeNodes(ItemsControl treeView, List<TreeNode> newNodes)
        {
            Console.WriteLine($"UpdateTreeNodes: Updating tree with {newNodes.Count} new nodes");

            var existingNodes = treeView.Items.Cast<TreeNode>().ToList();
            var nodesToKeep = new List<TreeNode>();
            var nodesToAdd = new List<TreeNode>(newNodes);

            foreach (var existingNode in existingNodes)
            {
                var matchingNode = newNodes.FirstOrDefault(n => n.Tag == existingNode.Tag);
                if (matchingNode != null)
                {
                    existingNode.Name = matchingNode.Name;
                    existingNode.Value = matchingNode.Value;
                    existingNode.IsExpanded = matchingNode.IsExpanded;
                    existingNode.FontSize = matchingNode.FontSize;
                    existingNode.Margin = matchingNode.Margin;
                    nodesToKeep.Add(existingNode);
                    nodesToAdd.Remove(matchingNode);
                    Console.WriteLine($"UpdateTreeNodes: Updated node with Tag={existingNode.Tag}, Name={existingNode.Name}");

                    UpdateTreeNodesForChildren(existingNode, matchingNode.Children);
                }
            }

            treeView.Items.Clear();
            foreach (var node in nodesToKeep.Concat(nodesToAdd))
            {
                treeView.Items.Add(node);
            }

            treeManager.ExpandAllTreeViewItems(treeView);
        }

        private void UpdateTreeNodesForChildren(TreeNode parentNode, List<TreeNode> newChildren)
        {
            var existingChildren = parentNode.Children.ToList();
            var childrenToKeep = new List<TreeNode>();
            var childrenToAdd = new List<TreeNode>(newChildren);

            foreach (var existingChild in existingChildren)
            {
                var matchingChild = newChildren.FirstOrDefault(c => c.Tag == existingChild.Tag);
                if (matchingChild != null)
                {
                    existingChild.Name = matchingChild.Name;
                    existingChild.Value = matchingChild.Value;
                    existingChild.IsExpanded = matchingChild.IsExpanded;
                    existingChild.FontSize = matchingChild.FontSize;
                    existingChild.Margin = matchingChild.Margin;
                    childrenToKeep.Add(existingChild);
                    childrenToAdd.Remove(matchingChild);
                    Console.WriteLine($"UpdateTreeNodesForChildren: Updated child node with Tag={existingChild.Tag}, Name={existingChild.Name}");

                    UpdateTreeNodesForChildren(existingChild, matchingChild.Children);
                }
            }

            parentNode.Children.Clear();
            foreach (var child in childrenToKeep.Concat(childrenToAdd))
            {
                parentNode.Children.Add(child);
            }
        }

        private void GenerateSchema(JObject jsonObject)
        {
            mainWindow.SchemaManager.GenerateSchema(jsonObject, mainWindow.SchemaCanvas, true);
        }

        private void UpdateLoadedFilesList(string filePath)
        {
            mainWindow.LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
            loadedFilePaths.Add(filePath);
            mainWindow.DefaultFileNameTextBlock.Text = filePath;

            bool hasFiles = mainWindow.LoadedFilesListBox.Items.Count > 0;
            mainWindow.EmptyFilesMessage.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            mainWindow.LoadedFilesListBox.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;
        }

        public void DisplaySelectedFileContent(object sender, SelectionChangedEventArgs e)
        {
            if (mainWindow.LoadedFilesListBox.SelectedItem is string selectedFileName)
            {
                string selectedFilePath = GetSelectedFilePath(selectedFileName);

                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    string fileContent = ReadFileContent(selectedFilePath);
                    if (fileContent != null)
                    {
                        DisplayFileContent(fileContent, selectedFilePath);
                    }
                    else
                    {
                        MessageBox.Show("Не удалось прочитать содержимое файла.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите файл из списка.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DisplayFileContent(string jsonContent, string filePath)
        {
            ClearPreviousData();

            mainWindow.DefaultFileNameTextBlock.Text = filePath;
            mainWindow.StepJsonTextBox.Text = jsonContent;

            string errorDescription;
            bool isValidJson = TryValidateJsonSyntax(jsonContent, out errorDescription);
            string processedContent = jsonContent;

            if (isValidJson && TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                FillTreeViewWithJsonNodes(jsonObject);
                GenerateSchema(jsonObject);
                mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowError(errorDescription);
            }
        }

        private string GetSelectedFilePath(string selectedFileName)
        {
            try
            {
                int index = mainWindow.LoadedFilesListBox.Items.IndexOf(selectedFileName);
                if (index >= 0 && index < loadedFilePaths.Count)
                {
                    return loadedFilePaths[index];
                }
                else
                {
                    MessageBox.Show("Выбранный файл не найден в списке загруженных файлов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при получении пути к файлу: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        public void UpdateJsonContent(string instanceId, string field, string value, List<JObject> instances)
        {
            Console.WriteLine($"UpdateJsonContent: instanceId={instanceId}, field={field}, value='{value}'");
            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(field))
            {
                Console.WriteLine("UpdateJsonContent: Invalid instanceId or field");
                return;
            }

            if (!TryConvertJsonToJObject(mainWindow.StepJsonTextBox.Text, out JObject jsonObject))
            {
                Console.WriteLine("UpdateJsonContent: Failed to parse JSON");
                return;
            }

            var instancesArray = jsonObject["instances"] as JArray;
            if (instancesArray == null)
            {
                Console.WriteLine("UpdateJsonContent: instances array is null");
                return;
            }

            var instance = instancesArray.FirstOrDefault(i => i["id"] != null && i["id"].ToString() == instanceId);
            if (instance == null)
            {
                Console.WriteLine($"UpdateJsonContent: instance not found for id={instanceId}");
                return;
            }

            var attributes = instance["attributes"] as JObject;
            if (attributes == null)
            {
                Console.WriteLine($"UpdateJsonContent: attributes is null for id={instanceId}");
                return;
            }

            if (instance["type"]?.ToString().Contains("product_definition") == true)
            {
                var formationId = attributes["formation"]?.ToString();
                var formation = instancesArray.FirstOrDefault(i => i["id"] != null && i["id"].ToString() == formationId);
                var productId = formation?["attributes"]?["of_product"]?.ToString();
                var product = productId != null ? instancesArray.FirstOrDefault(i => i["id"] != null && i["id"].ToString() == productId) : null;
                if (product != null && product["attributes"] != null)
                {
                    var productAttributes = product["attributes"] as JObject;
                    if (field == "name" || field == "id")
                    {
                        productAttributes[field] = value;
                        Console.WriteLine($"UpdateJsonContent: Updated product.attributes.{field}='{value}' for productId={productId}");
                    }
                    else if (field == "version")
                    {
                        if (formation?["attributes"] != null)
                        {
                            formation["attributes"]["id"] = value;
                            Console.WriteLine($"UpdateJsonContent: Updated formation.attributes.id='{value}' for formationId={formationId}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"UpdateJsonContent: product not found for productId={productId}");
                }
            }
            else if (instance["type"]?.ToString() == "eskd_product" || instance["type"]?.ToString() == "organization")
            {
                attributes[field] = value;
                Console.WriteLine($"UpdateJsonContent: Updated attributes.{field}='{value}' for instanceId={instanceId}");
            }

            string updatedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
            mainWindow.StepJsonTextBox.Text = updatedJson;

            FillTreeViewWithJsonNodes(jsonObject);
            mainWindow.SchemaManager.GenerateSchema(jsonObject, mainWindow.SchemaCanvas, false);
            mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
        }

        public void SaveFile(string action)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            try
            {
                string filePathToSave;

                if (action == "Сохранить")
                {
                    if (string.IsNullOrEmpty(currentFilePath))
                    {
                        MessageBox.Show("Сначала используйте 'Сохранить как' для выбора пути.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        if (saveFileDialog.ShowDialog() == true)
                        {
                            filePathToSave = saveFileDialog.FileName;
                            SaveToFile(filePathToSave);
                        }
                    }
                    else
                    {
                        SaveToFile(currentFilePath);
                    }
                }
                else if (action == "Сохранить как")
                {
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        filePathToSave = saveFileDialog.FileName;
                        SaveToFile(filePathToSave);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveToFile(string filePath)
        {
            try
            {
                string contentToSave = GetContentToSave();
                File.WriteAllText(filePath, contentToSave);
                currentFilePath = filePath;
                UpdateLoadedFilesList(currentFilePath);
                MessageBox.Show("Файл успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при записи файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetContentToSave()
        {
            return mainWindow.StepJsonTextBox.Text;
        }
    }
}