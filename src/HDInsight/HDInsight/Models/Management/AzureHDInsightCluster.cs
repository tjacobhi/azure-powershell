﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Management.HDInsight.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Microsoft.WindowsAzure.Commands.Common;

namespace Microsoft.Azure.Commands.HDInsight.Models
{
    public class AzureHDInsightCluster
    {
        public AzureHDInsightCluster(Cluster cluster)
        {
            Id = cluster.Id;
            Name = cluster.Name;
            Location = cluster.Location;
            ClusterVersion = cluster.Properties.ClusterVersion;
            OperatingSystemType = cluster.Properties.OsType ?? OSType.Linux;
            ClusterTier = cluster.Properties.Tier ?? Tier.Standard;
            ClusterState = cluster.Properties.ClusterState;
            ClusterType = cluster.Properties.ClusterDefinition.Kind;
            CoresUsed = cluster.Properties.QuotaInfo.CoresUsed ?? 0;
            var httpEndpoint =
                cluster.Properties.ConnectivityEndpoints?.FirstOrDefault(c => c.Name.Equals("HTTPS", StringComparison.OrdinalIgnoreCase));
            HttpEndpoint = httpEndpoint != null ? httpEndpoint.Location : null;
            Error = cluster.Properties.Errors?.Select(s => s.Message).FirstOrDefault();
            ResourceGroup = ClusterConfigurationUtils.GetResourceGroupFromClusterId(cluster.Id);
            ComponentVersion = new List<string>();
            if (cluster.Properties.ClusterDefinition.ComponentVersion != null && cluster.Properties.ClusterDefinition.ComponentVersion.Any())
            {
                foreach (var componentVersion in cluster.Properties.ClusterDefinition.ComponentVersion)
                {
                    ComponentVersion.Add(componentVersion.ToString());
                }
            }
            WorkerNodeDataDisksGroups = new List<DataDisksGroups>();
            if (cluster.Properties.ComputeProfile != null && cluster.Properties.ComputeProfile.Roles.Any())
            {
                var rolesWithDataDisksGroups = cluster.Properties.ComputeProfile.Roles.Where(x => x.DataDisksGroups != null);
                foreach (var role in rolesWithDataDisksGroups)
                {
                    WorkerNodeDataDisksGroups.AddRange(role.DataDisksGroups);
                }
            }
            var clusterSecurityProfile = cluster.Properties.SecurityProfile;
            SecurityProfile = clusterSecurityProfile != null ? new AzureHDInsightSecurityProfile()
            {
                Domain = clusterSecurityProfile.Domain,
                //We should not be returning the actual password to the user
                DomainUserCredential = new PSCredential(clusterSecurityProfile.DomainUsername, "***".ConvertToSecureString()),
                OrganizationalUnitDN = clusterSecurityProfile.OrganizationalUnitDN,
                LdapsUrls = clusterSecurityProfile.LdapsUrls != null ? clusterSecurityProfile.LdapsUrls.ToArray() : null,
                ClusterUsersGroupDNs = clusterSecurityProfile.ClusterUsersGroupDNs != null ? clusterSecurityProfile.ClusterUsersGroupDNs.ToArray() : null,
            } : null;

            MinSupportedTlsVersion = cluster.Properties.MinSupportedTlsVersion;
            DiskEncryption = cluster.Properties.DiskEncryptionProperties;
            AssignedIdentity = cluster.Identity;
            PublicNetworkAccessType = cluster.Properties?.NetworkSettings?.PublicNetworkAccess;
            OutboundPublicNetworkAccessType = cluster.Properties?.NetworkSettings?.OutboundOnlyPublicNetworkAccessType;
            EncryptionInTransit =cluster.Properties?.EncryptionInTransitProperties?.IsEncryptionInTransitEnabled;
            PrivateEndpoint = cluster.Properties?.ConnectivityEndpoints?.FirstOrDefault(endpoint => endpoint.Name.Equals("HTTPS-INTERNAL"))?.Location;
            var vnet = cluster.Properties?.ComputeProfile?.Roles?.FirstOrDefault(role => role.Name.Equals("workernode"))?.VirtualNetworkProfile;
            VirtualNetworkId = vnet?.Id;
            SubnetName = vnet?.Subnet;
        }

        public AzureHDInsightCluster(Cluster cluster, IDictionary<string, string> clusterConfiguration, IDictionary<string, string> clusterIdentity)
            : this(cluster)
        {
            if (clusterConfiguration != null)
            {
                var defaultAccount = ClusterConfigurationUtils.GetDefaultStorageAccountDetails(
                    cluster.Properties.ClusterVersion,
                    clusterConfiguration, 
                    clusterIdentity
                );

                if (defaultAccount != null)
                {
                    DefaultStorageAccount = defaultAccount.StorageAccountName;

                    var wasbAccount = defaultAccount as AzureHDInsightWASBDefaultStorageAccount;
                    var adlAccount = defaultAccount as AzureHDInsightDataLakeDefaultStorageAccount;

                    if (wasbAccount != null)
                    {
                        DefaultStorageContainer =  wasbAccount.StorageContainerName;
                    }
                    else if(adlAccount != null)
                    {
                        DefaultStorageRootPath = adlAccount.StorageRootPath;
                    }
                    else
                    {
                        DefaultStorageContainer = string.Empty;
                    }


                    AdditionalStorageAccounts = ClusterConfigurationUtils.GetAdditionStorageAccounts(clusterConfiguration, DefaultStorageAccount);
                }
            }
        }

        /// <summary>
        /// The name of the resource.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The ID of the resource.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The location of the resource.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// The version of the cluster.
        /// </summary>
        public string ClusterVersion { get; set; }

        /// <summary>
        /// The type of operating system.
        /// </summary>
        public OSType OperatingSystemType { get; set; }

        /// <summary>
        /// Gets or sets the cluster tier.
        /// </summary>
        public Tier ClusterTier { get; set; }

        /// <summary>
        /// The state of the cluster.
        /// </summary>
        public string ClusterState { get; set; }

        /// <summary>
        /// The type of cluster.
        /// </summary>
        public string ClusterType { get; set; }

        /// <summary>
        /// The cores used by the cluster.
        /// </summary>
        public int CoresUsed { get; set; }

        /// <summary>
        /// The endpoint with which to connect to the cluster.
        /// </summary>
        public string HttpEndpoint { get; set; }

        /// <summary>
        /// The error (if any).
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Default storage account for this cluster.
        /// </summary>
        public string DefaultStorageAccount { get; set; }

        /// <summary>
        /// Default storage container for this cluster.
        /// </summary>
        public string DefaultStorageContainer { get; set; }

        /// <summary>
        /// Default storage path where this Azure Data Lake Cluster is rooted
        /// </summary>
        public string DefaultStorageRootPath { get; set; }

        /// <summary>
        /// Default storage container for this cluster.
        /// </summary>
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Additional storage accounts for this cluster
        /// </summary>
        public List<string> AdditionalStorageAccounts { get; set; }

        /// <summary>
        /// Version of a component service in the cluster
        /// </summary>
        public List<string> ComponentVersion { get; set; }

        /// <summary>
        /// Data Disks Group Properties for the Worker Role.
        /// </summary>
        public List<DataDisksGroups> WorkerNodeDataDisksGroups { get; set; }
		
        /// Gets or sets the security profile.
        /// </summary>
        /// <value>
        /// The security profile.
        /// </value>
        public AzureHDInsightSecurityProfile SecurityProfile { get; set; }

        /// <summary>
        /// Gets or sets the minimal supported TLS version.
        /// </summary>
        public string MinSupportedTlsVersion { get; set; }

        /// <summary>
        /// Gets or sets the disk encryption properties.
        /// </summary>
        public DiskEncryptionProperties DiskEncryption { get; set; }

        /// <summary>
        /// Gets or sets the assigned identity.
        /// </summary>
        public ClusterIdentity AssignedIdentity { get; set; }

        /// <summary>
        /// Gets or sets the private endpoint.
        /// </summary>
        public string PrivateEndpoint;

        /// <summary>
        /// Gets or sets the public network access.
        /// </summary>
        public string PublicNetworkAccessType;

        /// <summary>
        /// Gets or sets the outbound public network access.
        /// </summary>
        public string OutboundPublicNetworkAccessType;

        /// <summary>
        /// Gets or sets the encryption in transit.
        /// </summary>
        public bool? EncryptionInTransit;

        /// <summary>
        /// Gets or sets the virtual network id.
        /// </summary>
        public string VirtualNetworkId;

        /// <summary>
        /// Gets or sets the subnet name.
        /// </summary>
        public string SubnetName;
    }
}
