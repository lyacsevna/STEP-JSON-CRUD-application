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
using System.Windows.Shapes;

namespace STEP_JSON_Application_for_ASKON
{
    public partial class MainWindow : Window
    {
        private string currentFilePath = string.Empty;
        private double scale = 1.0; // Текущий масштаб
        private const double ScaleRate = 0.1; // Шаг изменения масштаба

        public MainWindow()
        {
            InitializeComponent();
            ViewButton.IsChecked = true;

            // Подключаем обработчик события MouseWheel к SchemaCanvas
            SchemaCanvas.MouseWheel += SchemaCanvas_MouseWheel;
            // Применяем ScaleTransform к SchemaCanvas
            SchemaCanvas.RenderTransform = new ScaleTransform(scale, scale);
        }

        // Обработчик масштабирования
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
                    currentFilePath = filePath;
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

                        TextTabTreeView.Items.Clear();

                        foreach (var node in treeNodes)
                        {
                            TextTabTreeView.Items.Add(node);
                        }

                        ExpandAllTreeViewItems(TextTabTreeView);

                        GenerateSchema(jsonObject);
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

        private void GenerateSchema(JObject jsonObject)
        {
            SchemaCanvas.Children.Clear();

            if (jsonObject["instances"] == null)
            {
                MessageBox.Show("В JSON отсутствует массив 'instances'.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var instances = jsonObject["instances"].ToObject<List<JObject>>();
            if (instances == null || !instances.Any())
            {
                MessageBox.Show("Массив 'instances' пуст или некорректен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nodes = new Dictionary<string, (UIElement Element, double X, double Y)>();
            var relationships = new List<(string ParentId, string ChildId, string Label, string Type)>();

            foreach (var instance in instances)
            {
                string type = instance["type"]?.ToString() ?? "unknown";
                var attributes = instance["attributes"] as JObject;

                if (type.Contains("next_assembly_usage_occurrence") && attributes != null)
                {
                    string relatingId = attributes["relating_product_definition"]?.ToString();
                    string relatedId = attributes["related_product_definition"]?.ToString();
                    string refDesignator = attributes["reference_designator"]?.ToString() ?? "Входит в состав";
                    if (!string.IsNullOrEmpty(relatingId) && !string.IsNullOrEmpty(relatedId))
                    {
                        relationships.Add((relatingId, relatedId, refDesignator, "composition"));
                    }
                }
                else if (type == "eskd_organization_product_assignment" && attributes != null)
                {
                    string productId = attributes["assigned_product"]?.ToString();
                    string orgId = attributes["assigned_organization"]?.ToString();
                    string role = instances.FirstOrDefault(i => i["id"]?.ToString() == attributes["role"]?.ToString())?["attributes"]?["name"]?.ToString() ?? "Назначена организация";
                    if (!string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(orgId))
                    {
                        relationships.Add((productId, orgId, role, "organization"));
                    }
                }
                else if (type == "product_definition" && attributes != null)
                {
                    string defId = instance["id"]?.ToString();
                    string formationId = attributes["formation"]?.ToString();
                    if (!string.IsNullOrEmpty(defId) && !string.IsNullOrEmpty(formationId))
                    {
                        relationships.Add((defId, formationId, "Версия", "version"));
                    }
                }
            }

            var allIds = instances.Select(i => i["id"]?.ToString()).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
            var childIds = relationships.Select(r => r.ChildId).ToHashSet();
            var rootIds = allIds.Except(childIds).ToList();

            var levels = new Dictionary<int, List<(string Id, JObject Instance)>>();
            void BuildTree(string id, int level)
            {
                if (!levels.ContainsKey(level))
                    levels[level] = new List<(string, JObject)>();
                var instance = instances.FirstOrDefault(i => i["id"]?.ToString() == id);
                if (instance != null && !levels[level].Any(l => l.Id == id))
                {
                    levels[level].Add((id, instance));
                    var children = relationships.Where(r => r.ParentId == id).Select(r => r.ChildId);
                    foreach (var childId in children)
                    {
                        BuildTree(childId, level + 1);
                    }
                }
            }

            foreach (var rootId in rootIds)
            {
                BuildTree(rootId, 0);
            }

            const double nodeWidth = 200;
            const double nodeHeight = 100;
            const double verticalSpacing = 60;
            const double horizontalSpacing = 80;

            double maxCanvasWidth = 0;
            double maxCanvasHeight = 0;
            foreach (var level in levels.OrderBy(l => l.Key))
            {
                double levelX = level.Key * (nodeWidth + horizontalSpacing) + 20;
                double levelY = 20;

                foreach (var (id, instance) in level.Value)
                {
                    string type = instance["type"]?.ToString() ?? "unknown";
                    string label = $"ID: {id}\nType: {type}";

                    var node = CreateStyledRectangle(type, label, nodeWidth, nodeHeight);

                    Canvas.SetLeft(node, levelX);
                    Canvas.SetTop(node, levelY);
                    SchemaCanvas.Children.Add(node);
                    nodes[id] = (node, levelX, levelY);

                    levelY += nodeHeight + verticalSpacing;
                }

                maxCanvasWidth = Math.Max(maxCanvasWidth, levelX + nodeWidth);
                maxCanvasHeight = Math.Max(maxCanvasHeight, levelY);
            }

            SchemaCanvas.Width = Math.Max(SchemaCanvas.Width, maxCanvasWidth + 20);
            SchemaCanvas.Height = Math.Max(SchemaCanvas.Height, maxCanvasHeight + 20);

            foreach (var (parentId, childId, label, relType) in relationships)
            {
                if (nodes.ContainsKey(parentId) && nodes.ContainsKey(childId))
                {
                    var (parentNode, parentX, parentY) = nodes[parentId];
                    var (childNode, childX, childY) = nodes[childId];

                    double startX = parentX + nodeWidth;
                    double startY = parentY + nodeHeight / 2;
                    double endX = childX;
                    double endY = childY + nodeHeight / 2;
                    double midX = startX + (endX - startX) / 2;

                    var (line1, line2, line3, endShape) = CreateStyledConnection(relType, startX, startY, midX, endX, endY);

                    SchemaCanvas.Children.Add(line1);
                    SchemaCanvas.Children.Add(line2);
                    SchemaCanvas.Children.Add(line3);
                    SchemaCanvas.Children.Add(endShape);

                    var labelText = new TextBlock
                    {
                        Text = relType,
                        FontSize = 12,
                        Foreground = Brushes.Black,
                        Background = Brushes.White,
                        Padding = new Thickness(2),
                        TextAlignment = TextAlignment.Center
                    };
                    labelText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double labelWidth = labelText.DesiredSize.Width;
                    double labelHeight = labelText.DesiredSize.Height;

                    Canvas.SetLeft(labelText, midX - labelWidth / 2);
                    Canvas.SetTop(labelText, startY - labelHeight - 5);
                    SchemaCanvas.Children.Add(labelText);
                }
            }
        }

        private UIElement CreateStyledRectangle(string type, string label, double width, double height)
        {
            var textBlock = new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10),
                FontSize = 12,
                MaxWidth = width - 20,
                TextAlignment = TextAlignment.Center
            };

            var rectangle = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Black
            };

            switch (type)
            {
                case "eskd_product":
                    rectangle.StrokeThickness = 2;
                    break;
                case "eskd_product_definition_formation":
                    rectangle.StrokeThickness = 1;
                    break;
                case "product_definition":
                    rectangle.StrokeThickness = 1;
                    rectangle.StrokeDashArray = new DoubleCollection { 4, 2 };
                    break;
                case "organization":
                    rectangle.StrokeThickness = 2;
                    rectangle.StrokeDashArray = new DoubleCollection { 2, 2 };
                    break;
                default:
                    rectangle.StrokeThickness = 1;
                    break;
            }

            var container = new Canvas
            {
                Width = width,
                Height = height
            };
            container.Children.Add(rectangle);
            container.Children.Add(textBlock);

            textBlock.Measure(new Size(width - 20, double.PositiveInfinity));
            Canvas.SetLeft(textBlock, 10);
            Canvas.SetTop(textBlock, (height - textBlock.DesiredSize.Height) / 2);

            return container;
        }

        private (Line Line1, Line Line2, Line Line3, Shape EndShape) CreateStyledConnection(string relType, double startX, double startY, double midX, double endX, double endY)
        {
            var line1 = new Line { X1 = startX, Y1 = startY, X2 = midX, Y2 = startY };
            var line2 = new Line { X1 = midX, Y1 = startY, X2 = midX, Y2 = endY };
            var line3 = new Line { X1 = midX, Y1 = endY, X2 = endX, Y2 = endY };
            Shape endShape;

            endShape = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            switch (relType)
            {
                case "composition":
                    line1.Stroke = Brushes.Black;
                    line2.Stroke = Brushes.Black;
                    line3.Stroke = Brushes.Black;
                    line1.StrokeThickness = 2;
                    line2.StrokeThickness = 2;
                    line3.StrokeThickness = 2;
                    break;
                case "organization":
                    line1.Stroke = Brushes.Black;
                    line2.Stroke = Brushes.Black;
                    line3.Stroke = Brushes.Black;
                    line1.StrokeThickness = 1;
                    line2.StrokeThickness = 1;
                    line3.StrokeThickness = 1;
                    line1.StrokeDashArray = new DoubleCollection { 4, 2 };
                    line2.StrokeDashArray = new DoubleCollection { 4, 2 };
                    line3.StrokeDashArray = new DoubleCollection { 4, 2 };
                    break;
                case "version":
                    line1.Stroke = Brushes.Black;
                    line2.Stroke = Brushes.Black;
                    line3.Stroke = Brushes.Black;
                    line1.StrokeThickness = 1;
                    line2.StrokeThickness = 1;
                    line3.StrokeThickness = 1;
                    break;
                default:
                    line1.Stroke = Brushes.Black;
                    line2.Stroke = Brushes.Black;
                    line3.Stroke = Brushes.Black;
                    line1.StrokeThickness = 1;
                    line2.StrokeThickness = 1;
                    line3.StrokeThickness = 1;
                    break;
            }

            Canvas.SetLeft(endShape, endX - 5);
            Canvas.SetTop(endShape, endY - 5);

            return (line1, line2, line3, endShape);
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

        private void MainTabControl_SelectionChanged(object sender, RoutedEventArgs e)
        {
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