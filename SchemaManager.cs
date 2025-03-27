using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Shapes;

namespace STEP_JSON_Application_for_ASKON
{
    public class SchemaManager
    {
        public void GenerateSchema(JObject jsonObject, Canvas schemaCanvas)
        {

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
                    schemaCanvas.Children.Add(node);
                    nodes[id] = (node, levelX, levelY);

                    levelY += nodeHeight + verticalSpacing;
                }

                maxCanvasWidth = Math.Max(maxCanvasWidth, levelX + nodeWidth);
                maxCanvasHeight = Math.Max(maxCanvasHeight, levelY);
            }

            schemaCanvas.Width = Math.Max(schemaCanvas.Width, maxCanvasWidth + 20);
            schemaCanvas.Height = Math.Max(schemaCanvas.Height, maxCanvasHeight + 20);

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

                    schemaCanvas.Children.Add(line1);
                    schemaCanvas.Children.Add(line2);
                    schemaCanvas.Children.Add(line3);
                    schemaCanvas.Children.Add(endShape);

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
                    schemaCanvas.Children.Add(labelText);
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
    }
}
