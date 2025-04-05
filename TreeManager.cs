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

            var instances = json["instances"] as JArray;

            // Находим корневую сборку (Прижим)
            var rootProduct = instances.FirstOrDefault(i =>
                i["attributes"]?["id"]?.ToString() == "АБВГ.123456.001" &&
                i["type"]?.ToString() == "eskd_product");

            if (rootProduct != null)
            {
                var rootProductDef = instances.FirstOrDefault(i =>
                    i["type"]?.ToString() == "product_definition" &&
                    i["attributes"]?["formation"]?.ToString() == "#2");

                if (rootProductDef != null)
                {
                    var rootNode = CreateProductNode(rootProductDef, instances, true);
                    rootNodes.Add(rootNode);
                }
            }

            return rootNodes;
        }

        private TreeNode CreateProductNode(JToken productDef, JArray allInstances, bool isRoot = false)
        {
            string productId = productDef["id"]?.ToString();
            var attributes = productDef["attributes"] as JObject;
            string defId = attributes?["id"]?.ToString() ?? "Unknown Definition";
            string formationId = attributes?["formation"]?.ToString();

            var productDescription = GetProductDescription(productDef, allInstances);

            var productNode = new TreeNode
            {
                Name = isRoot ? $"# {defId}" : string.Empty,
                Value = productDescription,
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

                // Рекурсивно добавляем вложенные компоненты
                string relatedId = component["attributes"]?["related_product_definition"]?.ToString();
                var relatedProduct = allInstances.FirstOrDefault(i => i["id"]?.ToString() == relatedId);

                if (relatedProduct != null &&
                    relatedProduct["attributes"]?["formation"] != null)
                {
                    var nestedProductNode = CreateProductNode(relatedProduct, allInstances);
                    componentNode.Children.Add(nestedProductNode);
                }
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

            var componentNode = new TreeNode
            {
                Name = string.IsNullOrEmpty(refDesignator) ? "Состоит из" : $"Состоит из, поз.{refDesignator}",
                Value = $"кол-во {quantityLabel} {unit}",
                IsExpanded = true,
                FontSize = 14,
                Margin = new Thickness(10, 2, 0, 2)
            };

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

        // Остальные методы остаются без изменений
        private void ProcessInstances(JArray instances, TreeNode parentNode) { /* ... */ }
        private void ProcessFilteredAttributes(JObject attributes, TreeNode parentNode) { /* ... */ }
        private void ProcessJObject(JObject jObject, TreeNode parentNode) { /* ... */ }
        private void ProcessArray(JArray array, TreeNode parentNode) { /* ... */ }
        public void ExpandAllTreeViewItems(ItemsControl itemsControl) { /* ... */ }
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