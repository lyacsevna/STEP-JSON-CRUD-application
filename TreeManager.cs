using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace STEP_JSON_Application_for_ASKON
{
    public class TreeManager
    {
        public List<TreeNode> FormatJsonObject(JObject jsonObject)
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

                    JObject attributes = instance["attributes"] as JObject;
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
}
