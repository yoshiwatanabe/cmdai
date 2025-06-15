using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using System.Text.RegularExpressions;

namespace CmdAi.Core.Services;

public class AzureCommandResolver : ICommandResolver
{
    private readonly List<CommandPattern> _patterns;

    public AzureCommandResolver()
    {
        _patterns = new List<CommandPattern>
        {
            // Subscription management
            new(@"\b(list|show|get)\s+(subscriptions?|subs?)\b", "az account list --output table", "List all available subscriptions"),
            new(@"\b(current|show).*subscription\b", "az account show", "Show current subscription details"),
            new(@"\b(switch|set|change).*subscription\b", "az account set --subscription", "Switch to a specific subscription (you'll need to specify the subscription name/ID)"),
            new(@"\b(switch|set|change).*subscription\s+(.+)", "az account set --subscription \"$2\"", "Switch to the specified subscription"),
            
            // Resource group operations
            new(@"\b(list|show|get)\s+(resource\s*groups?|rg)\b", "az group list --output table", "List all resource groups"),
            new(@"\b(create|new)\s+(resource\s*group|rg)\s+(.+)", "az group create --name \"$3\" --location eastus", "Create a new resource group"),
            new(@"\b(delete|remove)\s+(resource\s*group|rg)\s+(.+)", "az group delete --name \"$3\" --yes", "Delete a resource group"),
            new(@"\b(list|show).*resources.*in\s+(.+)", "az resource list --resource-group \"$2\" --output table", "List resources in a specific resource group"),
            new(@"\b(list|show).*resources\b", "az resource list --output table", "List all resources in current subscription"),
            
            // Storage account operations
            new(@"\b(list|show|get)\s+(storage\s*accounts?|storage)\b", "az storage account list --output table", "List all storage accounts"),
            new(@"\b(create|new)\s+(storage\s*account|storage)\s+(.+)", "az storage account create --name \"$3\" --resource-group myResourceGroup --location eastus --sku Standard_LRS", "Create a new storage account"),
            new(@"\b(delete|remove)\s+(storage\s*account|storage)\s+(.+)", "az storage account delete --name \"$3\" --resource-group myResourceGroup --yes", "Delete a storage account"),
            new(@"\b(show|get)\s+(storage\s*account|storage)\s+(.+)", "az storage account show --name \"$3\" --resource-group myResourceGroup", "Show storage account details"),
            
            // Virtual machines
            new(@"\b(list|show|get)\s+(vms?|virtual\s*machines?)\b", "az vm list --output table", "List all virtual machines"),
            new(@"\b(start|boot)\s+(vm|virtual\s*machine)\s+(.+)", "az vm start --name \"$3\" --resource-group myResourceGroup", "Start a virtual machine"),
            new(@"\b(stop|shutdown)\s+(vm|virtual\s*machine)\s+(.+)", "az vm stop --name \"$3\" --resource-group myResourceGroup", "Stop a virtual machine"),
            new(@"\b(restart|reboot)\s+(vm|virtual\s*machine)\s+(.+)", "az vm restart --name \"$3\" --resource-group myResourceGroup", "Restart a virtual machine"),
            
            // App Service
            new(@"\b(list|show|get)\s+(webapps?|web\s*apps?|app\s*service)\b", "az webapp list --output table", "List all web apps"),
            new(@"\b(create|new)\s+(webapp|web\s*app)\s+(.+)", "az webapp create --name \"$3\" --resource-group myResourceGroup --plan myAppServicePlan", "Create a new web app"),
            new(@"\b(restart)\s+(webapp|web\s*app)\s+(.+)", "az webapp restart --name \"$3\" --resource-group myResourceGroup", "Restart a web app"),
            
            // Networking
            new(@"\b(list|show|get)\s+(vnets?|virtual\s*networks?)\b", "az network vnet list --output table", "List all virtual networks"),
            new(@"\b(list|show|get)\s+(subnets?)\b", "az network vnet subnet list --vnet-name myVNet --resource-group myResourceGroup --output table", "List subnets in a virtual network"),
            new(@"\b(list|show|get)\s+(nsgs?|network\s*security\s*groups?)\b", "az network nsg list --output table", "List network security groups"),
            
            // Key Vault
            new(@"\b(list|show|get)\s+(key\s*vaults?|keyvaults?)\b", "az keyvault list --output table", "List all key vaults"),
            new(@"\b(list|show|get)\s+(secrets?)\s+.*vault\s+(.+)", "az keyvault secret list --vault-name \"$3\" --output table", "List secrets in a key vault"),
            
            // Resource providers and locations
            new(@"\b(list|show|get)\s+(locations?|regions?)\b", "az account list-locations --output table", "List all available Azure regions"),
            new(@"\b(list|show|get)\s+(resource\s*providers?)\b", "az provider list --output table", "List all resource providers"),
            
            // Login and authentication
            new(@"\b(login|signin|authenticate)\b", "az login", "Login to Azure"),
            new(@"\b(logout|signout)\b", "az logout", "Logout from Azure"),
            new(@"\b(whoami|who\s*am\s*i|current\s*user)\b", "az account show --query user.name --output tsv", "Show current logged-in user"),
            
            // Extensions and configuration
            new(@"\b(list|show|get)\s+(extensions?)\b", "az extension list --output table", "List installed Azure CLI extensions"),
            new(@"\b(version|ver)\b", "az version", "Show Azure CLI version information"),
            new(@"\b(config|configuration)\b", "az config get", "Show current Azure CLI configuration")
        };
    }

    public bool CanResolve(string tool) => tool.Equals("az", StringComparison.OrdinalIgnoreCase) || 
                                          tool.Equals("azure", StringComparison.OrdinalIgnoreCase);

    public Task<CommandResult?> ResolveCommandAsync(CommandRequest request, CommandContext context)
    {
        if (!CanResolve(request.Tool))
            return Task.FromResult<CommandResult?>(null);

        var query = request.Query.ToLowerInvariant();

        foreach (var pattern in _patterns)
        {
            var match = pattern.Regex.Match(query);
            if (match.Success)
            {
                var command = pattern.Command;
                
                // Replace capture groups in the command
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    command = command.Replace($"${i}", match.Groups[i].Value.Trim());
                }

                // Add context information about Azure CLI requirement
                var contextInfo = "Requires Azure CLI (az) to be installed and authenticated";
                
                return Task.FromResult<CommandResult?>(
                    new CommandResult(command, pattern.Description, true, contextInfo));
            }
        }

        return Task.FromResult<CommandResult?>(null);
    }

    private record CommandPattern(string Pattern, string Command, string Description)
    {
        public Regex Regex { get; } = new(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}