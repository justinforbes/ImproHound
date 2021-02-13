﻿using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ImproHound.pages
{
    public partial class OUStructurePage : Page
    {
        private readonly MainWindow containerWindow;
        private readonly DBConnection connection;
        private readonly ConnectPage connectPage;
        private Dictionary<string, ADObject> forest;

        public OUStructurePage(MainWindow containerWindow, DBConnection connection, ConnectPage connectPage)
        {
            this.containerWindow = containerWindow;
            this.connection = connection;
            this.connectPage = connectPage;
            BuildOUStructure();
            InitializeComponent();
        }

        private async void BuildOUStructure()
        {
            Dictionary<string, ADObject> forestStructure = new Dictionary<string, ADObject>();

            List<IRecord> records;
            try
            {
                // TODO: Remember nodes without distinguishedname

                object output;
                records = await connection.Query(@"
                    MATCH (o)
                    WHERE NOT o.distinguishedname IS NULL
					UNWIND LABELS(o) AS adtype
                    WITH o.objectid AS objectid, o.distinguishedname AS distinguishedname, adtype 
                    WHERE adtype IN ['Domain', 'OU', 'Group', 'User', 'Computer', 'GPO']
                    RETURN objectid, distinguishedname, adtype ORDER BY size(distinguishedname) LIMIT 25
                ");
                if (!records[0].Values.TryGetValue("objectid", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong. Neo4j server response format is unexpected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    containerWindow.NavigateToPage(connectPage);
                    return;
                }
            }
            catch
            {
                // Authentication or connection error
                containerWindow.NavigateToPage(connectPage);
                return;
            }

            foreach (IRecord record in records)
            {
                //TODO: Make sure same object is not comming twice, if multiple labels
                record.Values.TryGetValue("objectid", out object objectid);
                record.Values.TryGetValue("distinguishedname", out object distinguishedname);
                record.Values.TryGetValue("adtype", out object type);

                bool gotTypeEnum = Enum.TryParse((string)type, out ADOjectType adType);
                if (!gotTypeEnum) adType = ADOjectType.Unknown;

                try
                {
                    if (adType.Equals(ADOjectType.Domain))
                    {
                        // TODO: Put sub domains under parent domain
                        ADObject adContainer = new ADObject((string)objectid, adType, (string)distinguishedname);
                        forestStructure.Add(adContainer.Distinguishedname, adContainer);
                    }
                    else
                    {
                        ADObject adObject = new ADObject((string)objectid, adType, (string)distinguishedname);
                        ADObject parent = GetParent(adObject, forestStructure);
                        string s = adObject.Distinguishedname.Substring(0, adObject.Distinguishedname.IndexOf(","));
                        parent.Members.Add(s, adObject);
                    }
                }
                catch
                {
                    Console.Error.WriteLine("Something went wrong when adding this AD object (objectid): " + objectid);
                }
            }
            Console.WriteLine("OU Structure build");
            forest = forestStructure;
            forestTreeView.ItemsSource = forest.Values.ToList();
        }

        private ADObject GetParent(ADObject adObject, Dictionary<string, ADObject> forestStructure)
        {
            // Find the domain the object belongs to
            // TODO: Handle if no domain / ou was not found
            foreach (KeyValuePair<string, ADObject> domain in forestStructure)
            {
                if (adObject.Distinguishedname.EndsWith(domain.Key))
                {
                    string[] oupath = adObject.Distinguishedname.Replace("," + domain.Key, "").Split(',');
                    adObject.Name = oupath[0].Substring(oupath[0].IndexOf("=") + 1);
                    ADObject parent = domain.Value;

                    if (oupath.Length > 1)
                    {
                        for (int i = oupath.Length - 1; i > 0; i--)
                        {
                            foreach (KeyValuePair<string, ADObject> container in parent.GetOUMembers())
                            {
                                if (oupath[i].Equals(container.Key))
                                {
                                    parent = container.Value;
                                    break;
                                }
                            }
                        }
                    }

                    return parent;
                }
            }
            throw new Exception("Error: Could not find ADObjects OU/Domain parent");
        }
    }

    public class ADObject
    {
        public ADObject(string objectid, ADOjectType type, string distinguishedname = null, string name = null, string tier = null)
        {
            Objectid = objectid;
            Name = name;
            Distinguishedname = distinguishedname;
            Tier = tier;
            Type = type;
            Members = new Dictionary<string, ADObject>();

            switch (type)
            {
                case ADOjectType.Domain:
                    Iconpath = "/resources/images/ad-icons/domain1.png";
                    break;
                case ADOjectType.OU:
                    Iconpath = "/resources/images/ad-icons/ou.png";
                    break;
                case ADOjectType.Group:
                    Iconpath = "/resources/images/ad-icons/group.png";
                    break;
                case ADOjectType.User:
                    Iconpath = "/resources/images/ad-icons/user.png";
                    break;
                case ADOjectType.Computer:
                    Iconpath = "/resources/images/ad-icons/computer.png";
                    break;
                case ADOjectType.GPO:
                    Iconpath = "/resources/images/ad-icons/gpo.png";
                    break;
                default:
                    Iconpath = "/resources/images/ad-icons/domain2.png";
                    break;
            }
        }
        public string Objectid { get; set; }
        public string Name { get; set; }
        public string Distinguishedname { get; set; }
        public ADOjectType Type { get; set; }
        public string Tier { get; set; }
        public string Iconpath { get; set; }

        public Dictionary<string, ADObject> Members { get; set; }

        public List<ADObject> MemberList => Members.Values.ToList();

        public Dictionary<string, ADObject> GetOUMembers()
        {
            Dictionary<string, ADObject> ous = new Dictionary<string, ADObject>();
            foreach (KeyValuePair<string, ADObject> member in Members)
            {
                if (member.Value.Type is ADOjectType.OU)
                {
                    ous.Add(member.Key, member.Value);
                }
            }
            return ous;
        }
    }

    public class Tiers : List<String>
    {
        public Tiers()
        {
            AddRange(new List<String>() { "0", "1", "2" });
        }
    }

    public enum ADOjectType
    {
        Unknown, Domain, OU, Group, User, Computer, GPO
    }
}