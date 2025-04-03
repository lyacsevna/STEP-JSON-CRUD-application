using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace STEP_JSON_Application_for_ASKON
{
    public class TreeManager
    {
        public List<TreeNode> FormatJsonObject(JObject jsonObject) => FormatJson(jsonObject);

        public List<TreeNode> FormatJson(JObject json)
        {
            var rootNodes = new List<TreeNode>();

            if (json["instances"] == null)
            {
                // Если нет instances, обрабатываем как обычный JSON
                var rootNode = new TreeNode
                {
                    Name = "=== ДАННЫЕ ===",
                    IsExpanded = true,
                    FontSize = 14
                };
                ProcessJObject(json, rootNode);
                rootNodes.Add(rootNode);
                return rootNodes;
            }

            // Создаем узлы для продукта и его компонентов
            var instances = json["instances"] as JArray;
            var productDefinitions = instances.Where(i => i["type"]?.ToString()?.Contains("product_definition") == true).ToList();

            foreach (var productDef in productDefinitions)
            {
                var productNode = CreateProductNode(productDef, instances);
                rootNodes.Add(productNode);
            }

            return rootNodes;
        }
        private TreeNode CreateProductNode(JToken productDef, JArray allInstances)
        {
            string productId = productDef["id"]?.ToString();
            var attributes = productDef["attributes"] as JObject;
            string defId = attributes?["id"]?.ToString() ?? "Unknown Definition";
            string formationId = attributes?["formation"]?.ToString();

            var productNode = new TreeNode
            {
                Name = $"# {defId}",
                Value = GetProductDescription(productDef, allInstances),
                IsExpanded = true,
                FontSize = 14,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Добавляем версию
            if (!string.IsNullOrEmpty(formationId))
            {
                var versionInstance = allInstances.FirstOrDefault(i => i["id"]?.ToString() == formationId);
                if (versionInstance != null)
                {
                    var versionNode = new TreeNode
                    {
                        Name = "Версия",
                        Value = GetVersionDescription(versionInstance),
                        FontSize = 14,
                        Margin = new Thickness(10, 2, 0, 2)
                    };
                    productNode.Children.Add(versionNode);
                }
            }

            // Добавляем компоненты
            var components = allInstances
                .Where(i => i["type"]?.ToString()?.Contains("next_assembly_usage_occurrence") == true &&
                            i["attributes"]?["relating_product_definition"]?.ToString() == productId)
                .ToList();

            foreach (var component in components)
            {
                var componentNode = CreateComponentNode(component, allInstances);
                productNode.Children.Add(componentNode);
            }

            return productNode;
        }
        private TreeNode CreateComponentNode(JToken component, JArray allInstances)
        {
            var attributes = component["attributes"] as JObject;
            string relatedId = attributes?["related_product_definition"]?.ToString();
            string refDesignator = attributes?["reference_designator"]?.ToString();
            string quantityId = attributes?["quantity"]?.ToString();
            string quantityLabel = GetQuantityLabel(quantityId, allInstances);
            string unit = GetUnitForQuantity(quantityId, allInstances);

            var relatedProduct = allInstances.FirstOrDefault(i => i["id"]?.ToString() == relatedId);

            var componentNode = new TreeNode
            {
                Name = string.IsNullOrEmpty(refDesignator) ? "Состоит из" : $"Состоит из, поз.{refDesignator}",
                Value = $"кол-во {quantityLabel} {unit}",
                IsExpanded = true,
                FontSize = 14,
                Margin = new Thickness(10, 2, 0, 2)
            };

            if (relatedProduct != null)
            {
                var productDetails = GetProductDescription(relatedProduct, allInstances);
                var detailsNode = new TreeNode
                {
                    Name = productDetails,
                    FontSize = 14,
                    Margin = new Thickness(15, 2, 0, 2)
                };
                componentNode.Children.Add(detailsNode);
            }

            return componentNode;
        }

        private string GetProductDescription(JToken productInstance, JArray allInstances)
        {
            var attributes = productInstance["attributes"] as JObject;
            string type = productInstance["type"]?.ToString() ?? "unknown";

            if (type.Contains("product_definition"))
            {
                string defId = attributes?["id"]?.ToString() ?? "Unknown Definition";
                string formationId = attributes?["formation"]?.ToString();

                if (!string.IsNullOrEmpty(formationId))
                {
                    var formation = allInstances.FirstOrDefault(i => i["id"]?.ToString() == formationId);
                    string productId = formation?["attributes"]?["of_product"]?.ToString();
                    string version = formation?["attributes"]?["id"]?.ToString() ?? "Unknown Version";

                    if (!string.IsNullOrEmpty(productId))
                    {
                        var product = allInstances.FirstOrDefault(i => i["id"]?.ToString() == productId);
                        string productCode = product?["attributes"]?["id"]?.ToString() ?? "Unknown Product";
                        string productName = product?["attributes"]?["name"]?.ToString() ?? "";
                        return $"{productCode} {productName} версия {version}".Trim();
                    }
                    return $"{defId} версия {version}";
                }
                return defId;
            }
            return attributes?["name"]?.ToString() ?? "Unknown Product";
        }

        private string GetVersionDescription(JToken versionInstance)
        {
            var attributes = versionInstance["attributes"] as JObject;
            return attributes?["id"]?.ToString() ?? "Unknown Version";
        }

        private string GetQuantityLabel(string quantityId, JArray allInstances)
        {
            if (string.IsNullOrEmpty(quantityId)) return "неизвестно";

            var quantity = allInstances.FirstOrDefault(i =>
                i["id"]?.ToString() == quantityId &&
                i["type"]?.ToString() == "measure_with_unit");

            return quantity?["attributes"]?["value_component"]?.ToString()?.Trim() ?? "неизвестно";
        }

        private string GetUnitForQuantity(string quantityId, JArray allInstances)
        {
            if (string.IsNullOrEmpty(quantityId)) return "шт.";

            var quantity = allInstances.FirstOrDefault(i =>
                i["id"]?.ToString() == quantityId &&
                i["type"]?.ToString() == "measure_with_unit");
            if (quantity == null) return "шт.";

            string unitId = quantity["attributes"]?["unit_component"]?.ToString();
            var unit = allInstances.FirstOrDefault(i =>
                i["id"]?.ToString() == unitId &&
                i["type"]?.ToString() == "context_dependent_unit");

            return unit?["attributes"]?["id"]?.ToString() ?? "шт.";
        }
        private void ProcessInstances(JArray instances, TreeNode parentNode)
        {
            foreach (var instance in instances)
            {
                var instanceNode = new TreeNode
                {
                    Name = $"ID: {instance["id"]}",
                    Value = $"Тип: {instance["type"]}",
                    IsExpanded = true,
                    FontSize = 14,
                    Margin = new Thickness(10, 3, 0, 3)
                };

                if (instance["attributes"] is JObject attributes)
                {
                    var attrsNode = new TreeNode
                    {
                        Name = "Атрибуты",
                        IsExpanded = true,
                        FontSize = 14
                    };
                    ProcessFilteredAttributes(attributes, attrsNode);
                    instanceNode.Children.Add(attrsNode);
                }

                parentNode.Children.Add(instanceNode);
            }
        }

        private void ProcessFilteredAttributes(JObject attributes, TreeNode parentNode)
        {
            var allowedAttributes = new HashSet<string> { "id", "name", "description" };

            foreach (var property in attributes.Properties())
            {
                if (!allowedAttributes.Contains(property.Name.ToLower()))
                    continue;

                var node = new TreeNode
                {
                    FontSize = 14,
                    Margin = new Thickness(15, 2, 0, 2)
                };

                if (property.Name.ToLower() == "description")
                {
                    node.ImageSource = new BitmapImage(new Uri("pack://application:,,,/StaticFiles/description.png"));
                    node.Name = string.Empty;
                }
                else
                {
                    node.Name = property.Name;
                }

                switch (property.Value.Type)
                {
                    case JTokenType.Object:
                        node.Value = "{...}";
                        node.IsExpanded = true;
                        ProcessJObject((JObject)property.Value, node);
                        break;
                    case JTokenType.Array:
                        node.Value = $"[{property.Value.Count()}]";
                        node.IsExpanded = true;
                        ProcessArray((JArray)property.Value, node);
                        break;
                    default:
                        node.Value = property.Value.ToString();
                        break;
                }

                parentNode.Children.Add(node);
            }
        }

        private void ProcessJObject(JObject jObject, TreeNode parentNode)
        {
            foreach (var property in jObject.Properties())
            {
                var node = new TreeNode
                {
                    FontSize = 14,
                    Margin = new Thickness(15, 2, 0, 2)
                };

                if (property.Name.ToLower() == "description")
                {
                    node.ImageSource = new BitmapImage(new Uri("pack://application:,,,/StaticFiles/description.png"));
                    node.Name = string.Empty;
                }
                else
                {
                    node.Name = property.Name;
                }

                switch (property.Value.Type)
                {
                    case JTokenType.Object:
                        node.Value = "{...}";
                        node.IsExpanded = true;
                        ProcessJObject((JObject)property.Value, node);
                        break;
                    case JTokenType.Array:
                        node.Value = $"[{property.Value.Count()}]";
                        node.IsExpanded = true;
                        ProcessArray((JArray)property.Value, node);
                        break;
                    default:
                        node.Value = property.Value.ToString();
                        break;
                }

                parentNode.Children.Add(node);
            }
        }

        private void ProcessArray(JArray array, TreeNode parentNode)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var node = new TreeNode
                {
                    Name = $"[{i}]",
                    FontSize = 14,
                    Margin = new Thickness(15, 2, 0, 2)
                };

                var item = array[i];
                if (item is JObject obj)
                {
                    node.Value = "{...}";
                    node.IsExpanded = true;
                    ProcessJObject(obj, node);
                }
                else if (item is JArray arr)
                {
                    node.Value = $"[{arr.Count}]";
                    node.IsExpanded = true;
                    ProcessArray(arr, node);
                }
                else
                {
                    node.Value = item.ToString();
                }

                parentNode.Children.Add(node);
            }
        }

        public void ExpandAllTreeViewItems(ItemsControl itemsControl)
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
        public BitmapImage ImageSource { get; set; }
        public int FontSize { get; set; } = 12;
        public Thickness Margin { get; set; } = new Thickness(5);
    }
}