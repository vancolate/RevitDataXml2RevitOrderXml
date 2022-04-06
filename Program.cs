using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Linq;

using static System.Console;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace RevitDataXml2RevitOrderXml
{
    static class XmlFactory
    {
        private static XDocument Xdocument = new XDocument();
        private static XElement root = new XElement("Root");
        static XmlFactory()
        {
            Xdocument.Add(root);
        }

        public static void SaveXml(string savePath)
        {
            Xdocument.Save(savePath);
        }

        public static void AppendXml(PipeNodeBase pipeNodeBase)
        {
            XElement pipeGroupXml;
            XElement pipeListXml;
            XElement pipeXml;

            int groupCount = 1;
            foreach (var pipeGroup in pipeNodeBase._pipeGroups)
            {
                pipeGroupXml = new XElement("PipeGroup");
                int listCount = 1;
                //遍历组内所有管件列
                foreach (var pipeList in pipeGroup._pipeLists)
                {
                    pipeListXml = new XElement("PipeList");
                    int nodeCount = 1;
                    //遍历列内所有管件(行)
                    foreach (var pipe in pipeList._pipes)
                    {
                        pipeXml = new XElement("Pipe");
                        pipeXml.SetAttributeValue("UniqueId", $"{pipe.uid}");
                        pipeXml.SetAttributeValue("StartPoint", $"{pipe.startPoint}");
                        pipeXml.SetAttributeValue("EndPoint", $"{pipe.endPoint}");
                        pipeXml.SetAttributeValue("Width", $"{pipe.width}");
                        pipeXml.SetAttributeValue("Height", $"{pipe.height}");

                        pipeListXml.Add(pipeXml);
                        //下一行/下一个管件
                        nodeCount++;
                    }

                    pipeListXml.SetAttributeValue("PipeListNo", $"{listCount}");
                    pipeListXml.SetAttributeValue("PrevListNo", $"{pipeGroup._pipeLists.FindIndex((elem) => { return ReferenceEquals(elem, pipeList.prev); }) + 1}");
                    pipeListXml.SetAttributeValue("TeeId_Next", $"{pipeList.teeId_next}");
                    pipeListXml.SetAttributeValue("TeePoint_Next", $"{pipeList.teePoint_next}");

                    pipeListXml.SetAttributeValue("FamilyName", $"{pipeList.familyName}");
                    pipeListXml.SetAttributeValue("SymbolName", $"{pipeList.symbolName}");
                    pipeListXml.SetAttributeValue("SystemClassfy", $"{pipeList.systemClassfy}");
                    pipeListXml.SetAttributeValue("SystemType", $"{pipeList.systemType}");
                    pipeListXml.SetAttributeValue("HorizonOffset", $"{pipeList.horizonOffset}");
                    pipeListXml.SetAttributeValue("VerticalOffset", $"{pipeList.verticalOffset}");
                    pipeListXml.SetAttributeValue("Color", $"{pipeList.color}");

                    if (pipeGroup.nodeType == NodeType.Duct)
                        pipeListXml.SetAttributeValue("DuctType", $"{pipeList.ductType}");

                    pipeGroupXml.Add(pipeListXml);
                    //下一个管件列
                    listCount++;
                }
                pipeGroupXml.SetAttributeValue("PipeGroupNo", $"{groupCount}");
                pipeGroupXml.SetAttributeValue("Type", $"{pipeGroup.nodeType}");

                root.Add(pipeGroupXml);
                //下一个管件组
                groupCount++;
            }

            return;
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            //①找到xml
            string xmlReadPath = args[0];
            string xmlWritePath = args[1];
            WriteLine($"xml读取路径为:{xmlReadPath}");
            WriteLine($"xml输出路径为:{xmlWritePath}");

            if (args.Length < 2)
            {
                WriteLine("缺少参数:xml读取路径或xml输出路径");
                return;
            }

            if (!File.Exists(xmlReadPath))
            {
                WriteLine("xml读取路径不存在或不正确");
                return;
            }

            //②读取xml
            WriteLine("载入xml中...");
            XDocument xDocument = XDocument.Load(xmlReadPath);
            var root = xDocument.Root;

            var entitys = root.Element("Entitys").Elements();
            var fittings = root.Element("Fittings").Elements();

            //③+④+⑤
            Process_AND_AppendXml_WithOneType(NodeType.Pipe,entitys, fittings);
            Process_AND_AppendXml_WithOneType(NodeType.Duct,entitys, fittings);

            //⑤
            XmlFactory.SaveXml(xmlWritePath);

            WriteLine("程序结束");
            return;
        }

        private static void Process_AND_AppendXml_WithOneType(NodeType nodeType,IEnumerable<XElement> entitys, IEnumerable<XElement> fittings)
        {

            //(1)Duct✓ (2)Pipe (3)LineDuct (4)LinePipe
            var entitys_duct = GetTypeEntitysFromAllEntitys(nodeType.ToString(), entitys);
            var fittings_duct = GetTypeFittingsFromAllFittings(entitys_duct, fittings);

            //③转化为自定义管道类
            //第一次获取连接数据,将xml化为内存对象.以及管道自己的数据.
            List<PipeNode> OriginalPipeNodes = GetPipeNodeFromDate(entitys_duct, fittings_duct, nodeType);

            //④处理管道类
            //去除附件,直连两端
            DeleteFittingsNode(OriginalPipeNodes);

            PipeNodeBase pipeNodeBase = Output2Base(OriginalPipeNodes);

            //第二次获取属性数据,从管道上获取,放到列、组
            foreach (var pipeGroup in pipeNodeBase._pipeGroups)
            {
                {
                    var someonePipeXml = entitys_duct.First(elem => elem.Attribute("UniqueId").Value == pipeGroup._pipeLists[0]._pipes[0].uid);
                    //在这里配置组拥有的属性
                    pipeGroup.nodeType = nodeType;//Enum.Parse<NodeType>(someonePipeXml.Attribute("type").Value);
                }

                foreach (var pipeList in pipeGroup._pipeLists)
                {
                    var someonePipeXml = entitys_duct.First(elem => elem.Attribute("UniqueId").Value == pipeList._pipes[0].uid);
                    //在这里配置列拥有的属性
                    pipeList.familyName = someonePipeXml.Element("FamilyName").FirstAttribute.Value;
                    pipeList.symbolName = someonePipeXml.Element("SymbolName").FirstAttribute.Value;
                    pipeList.horizonOffset = someonePipeXml.Element("HorizonOffset").FirstAttribute.Value;
                    pipeList.verticalOffset = someonePipeXml.Element("VerticalOffset").FirstAttribute.Value;
                    pipeList.systemClassfy = someonePipeXml.Element("SystemClassfy").FirstAttribute.Value;
                    pipeList.systemType = someonePipeXml.Element("SystemType").FirstAttribute.Value;
                    pipeList.color = someonePipeXml.Element("Color").FirstAttribute.Value;

                    if (pipeGroup.nodeType == NodeType.Duct)
                        pipeList.ductType = someonePipeXml.Element("DuctType").Attribute("value").Value;
                }
            }

            //⑤输出RevitOrder.xml
            XmlFactory.AppendXml(pipeNodeBase);
        }




        //必须由管道开始遍历,且管道的一端为空或所连id为空,且管道不能有重复
        //调用Output2Group,加入pipeNodeBase中
        private static PipeNodeBase Output2Base(List<PipeNode> originalPipeNodes)
        {
            PipeNodeBase pipeNodeBase = new PipeNodeBase();
            //遍历起点
            bool findNode;
            do
            {
                findNode = false;
                foreach (PipeNode pipeNode in originalPipeNodes)
                {
                    //必须由管道开始
                    //if (pipeNode.counted)
                    if (pipeNode.counted || pipeNode.pipeNodeType != PipeNodeType.Pipe)
                        continue;

                    foreach (NodeConnector connector in pipeNode.connectors)
                    {
                        //找到一个起点: 存在一端为空
                        if (connector == null || connector.node == null)
                            findNode = true;
                    }
                    if (!findNode)
                        continue;

                    PipeNodeGroup pipeNodeGroup = new PipeNodeGroup();
                    //顺序排入
                    Output2Group(pipeNode, pipeNodeGroup._pipeLists);

                    //加入节点库
                    if (pipeNodeGroup._pipeLists.Count > 0)
                        pipeNodeBase._pipeGroups.Add(pipeNodeGroup);
                    break;
                }
            } while (findNode);
            return pipeNodeBase;
        }

        private static void Output2Group(PipeNode firstNode, List<PipeNodeList> pipeNodeGroup)
        {
            if (firstNode.counted)
                return;

            PipeNodeList prevList;
            PipeNodeList pipeNodeList = new PipeNodeList();

            PipeNode current;
            PipeNode prev;

            Stack<(PipeNode, PipeNode, PipeNodeList)> nextStack = new Stack<(PipeNode, PipeNode, PipeNodeList)>();
            nextStack.Push((firstNode, null, null));

            do
            {
                (current, prev, prevList) = nextStack.Pop();

                if (current.counted)
                {
                    if (pipeNodeList._pipes.Count > 0)
                    {
                        pipeNodeList.prev = prevList;
                        if (pipeNodeList._pipes.Count > 0)
                            pipeNodeGroup.Add(pipeNodeList);
                        pipeNodeList = new PipeNodeList() { };
                    }
                    continue;
                }
                current.counted = true;
                //WPFConsole($"{current.id}");

                if (current.pipeNodeType == PipeNodeType.Pipe)
                {
                    var connected = false;
                    foreach (NodeConnector connector in current.connectors)
                    {
                        if (connector == null || connector.node == null || connector.node == prev)
                            continue;
                        connected = true;
                        nextStack.Push((connector.node, current, prevList));
                        break;
                    }
                    pipeNodeList._pipes.Add(current);
                    if (!connected)
                    {
                        //加入
                        pipeNodeList.prev = prevList;
                        if (pipeNodeList._pipes.Count > 0)
                            pipeNodeGroup.Add(pipeNodeList);
                        pipeNodeList = new PipeNodeList();
                    }
                }
                else if (current.pipeNodeType == PipeNodeType.Tee || current.pipeNodeType == PipeNodeType.Four)
                {
                    //加入
                    pipeNodeList.prev = prevList;
                    pipeNodeList.teeId_next = current.uid;
                    pipeNodeList.teePoint_next = current.startPoint;
                    if (pipeNodeList._pipes.Count > 0)
                        pipeNodeGroup.Add(pipeNodeList);
                    //分支循环
                    foreach (NodeConnector connector in current.connectors)
                    {
                        if (connector == null || connector.node == null || connector.node == prev)
                            continue;
                        nextStack.Push((connector.node, current, pipeNodeList));
                        continue;
                    }
                    pipeNodeList = new PipeNodeList();
                }
                else if (current.pipeNodeType == PipeNodeType.Single)
                    continue;
                else
                    throw new Exception("current.pipeNodeType == ?");

            } while (nextStack.Count > 0);

        }
        private static void DeleteFittingsNode(List<PipeNode> originalPipeNodes)
        {
            foreach (PipeNode pipeNode in originalPipeNodes)
            {
                //(可能)是附件,才能去除附件
                if (pipeNode.pipeNodeType != PipeNodeType.Bend)
                    continue;

                //不要算入列表
                pipeNode.counted = true;

                //超过3+连接点 异常
                if (pipeNode.FirstNullIndex > 2)
                {
                    throw new Exception($"{pipeNode.uid}:error:这个应去除的附件连接口超过2个");
                }

                int unNullConnects = 0;
                foreach (NodeConnector connector in pipeNode.connectors)
                {
                    if (connector == null || connector.node == null)
                        continue;
                    unNullConnects++;
                }

                if (unNullConnects == 1)
                {
                    //存在1个连接,去除连接
                    NodeConnector firstConnector = pipeNode.connectors[0];
                    if (firstConnector.node == null)
                    {
                        throw new Exception("找错了:1");
                    }

                    NodeConnector firstGetConnector = null;
                    foreach (NodeConnector first_Connector in firstConnector.node.connectors)
                    {
                        if (first_Connector == null)
                            break;
                        if (Object.ReferenceEquals(pipeNode, first_Connector.node))
                        {
                            firstGetConnector = first_Connector;
                            break;
                        }
                    }

                    //来了来了
                    firstGetConnector.node = null;
                    firstGetConnector.node_uid = String.Empty;
                }
                if (unNullConnects == 2) //==2
                {
                    //仅且存在两个连接,才能直连链表
                    NodeConnector firstConnector = pipeNode.connectors[0];
                    NodeConnector secondConnector = pipeNode.connectors[1];
                    //仅且存在两个连接,才能直连链表
                    if (firstConnector.node == null || secondConnector.node == null)
                    {
                        throw new Exception("找错了:2");
                    }

                    NodeConnector firstGetConnector = null;
                    NodeConnector secondGetConnector = null;

                    foreach (NodeConnector first_Connector in firstConnector.node.connectors)
                    {
                        if (first_Connector == null)
                            break;
                        if (Object.ReferenceEquals(pipeNode, first_Connector.node))
                        {
                            firstGetConnector = first_Connector;
                            break;
                        }
                    }
                    foreach (NodeConnector second_Connector in secondConnector.node.connectors)
                    {
                        if (second_Connector == null)
                            break;
                        if (Object.ReferenceEquals(pipeNode, second_Connector.node))
                        {
                            secondGetConnector = second_Connector;
                            break;
                        }
                    }
                    //if (firstGetConnector == null || secondGetConnector == null)
                    //    throw new Exception("我能连到你,你却连不到我??");

                    //来了来了
                    firstGetConnector.node = secondConnector.node;
                    firstGetConnector.node_uid = secondConnector.node_uid;
                    secondGetConnector.node = firstConnector.node;
                    secondGetConnector.node_uid = firstConnector.node_uid;
                }
            }
        }

        //由entitys_duct提出管道,由fittings_duct提出管件,组成包含所有元素的列,然后在里面调用id连接,使自定义管道实例化完成
        private static List<PipeNode> GetPipeNodeFromDate(IEnumerable<XElement> entitys_duct, IEnumerable<XElement> fittings_duct, NodeType nodeType)
        {
            List<PipeNode> OriginalPipeNodes = new List<PipeNode>();

            foreach (var entity in entitys_duct)
            {
                //为当前管道xml新建管道实例
                PipeNode pipeNode = new PipeNode()
                {
                    pipeNodeType = PipeNodeType.Pipe,
                    uid = entity.Attribute("UniqueId").Value,
                    //在这里配置管道应该获得的属性(宽高在下一段位置获取)
                    startPoint= entity.Element("LocationEnt").Attribute("StartPoint").Value,
                    endPoint= entity.Element("LocationEnt").Attribute("EndPoint").Value,

                };
                //获取宽高
                switch (nodeType)
                {
                    case NodeType.Duct:
                        if (entity.Element("DuctType").Attribute("value").Value == "圆形")
                            pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("Radius").FirstAttribute.Value) * 2).ToString();
                        else
                        {
                            pipeNode.width = entity.Element("DuctType").Attribute("Length1").Value;
                            pipeNode.height = entity.Element("DuctType").Attribute("Length2").Value;
                        }
                        break;
                    case NodeType.Pipe:
                        pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("Radius").FirstAttribute.Value) * 2).ToString();
                        break;
                    case NodeType.LineDuct:
                    case NodeType.LinePipe:
                        break;
                }

                //加入数组
                OriginalPipeNodes.Add(pipeNode);
            }

            foreach (var fitting in fittings_duct)
            {
                PipeNode pipeNode = new PipeNode()
                {
                    uid = fitting.Attribute("UniqueId").Value,
                    startPoint= fitting.Attribute("Point").Value,
                };
                var connectedUids = fitting.Attribute("ConnectorEntitys").Value.Split(";");
                switch (connectedUids.Length)
                {
                    case 1:
                        pipeNode.pipeNodeType = PipeNodeType.Single;
                        break;
                    case 2:
                        pipeNode.pipeNodeType = PipeNodeType.Bend;
                        break;
                    case 3:
                        pipeNode.pipeNodeType = PipeNodeType.Tee;
                        break;
                    case 4:
                        pipeNode.pipeNodeType = PipeNodeType.Four;
                        break;
                    default:
                        throw new Exception($"连接器数量错误:connectedCount={connectedUids.Length},uid={pipeNode.uid}");
                }

                //互联管道节点id
                ConnectPipeNodeId(connectedUids, pipeNode);

                //加入数组
                OriginalPipeNodes.Add(pipeNode);
            }

            //连接节点
            ConnectPipeNode(OriginalPipeNodes);

            return OriginalPipeNodes;
        }

        private static PipeNode FindId(string uid, List<PipeNode> pipeNodes)
        {
            PipeNode pipeNode = pipeNodes.FirstOrDefault((node) => { return node.uid == uid; });
            if (pipeNode != null)
                return pipeNode;
            else
                return null;
        }
        private static void ConnectPipeNode(List<PipeNode> pipeNodes)
        {
            foreach (PipeNode pipeNode in pipeNodes)
            {
                for (int i = 0; i < pipeNode.FirstNullIndex; i++)
                    if (pipeNode.connectors[i].node_uid != String.Empty)
                    {
                        //正向连接
                        PipeNode neighbour = FindId(pipeNode.connectors[i].node_uid, pipeNodes);

                        if (neighbour == null)
                            throw new Exception($"ConnectPipeNode时邻居为空:我的uid={pipeNode.uid},查找邻居uid={pipeNode.connectors[i].node_uid}");

                        pipeNode.connectors[i].node = neighbour;

                        //反向连接
                        bool exist = false;
                        for (int j = 0; j < neighbour.FirstNullIndex; j++)
                        {
                            if (neighbour.connectors[j].node_uid == pipeNode.uid)
                            {
                                exist = true;
                                break;
                            }
                        }
                        if (!exist)
                        {
                            var nc = neighbour.CreateNodeConnector();
                            nc.node_uid = pipeNode.uid;
                            nc.node = pipeNode;
                        }
                    }
            }
        }

        private static void ConnectPipeNodeId(string[] connectedUids, PipeNode pipeNode)
        {
            //连接节点
            foreach (string connectedUid in connectedUids)
                //变得超简单
                pipeNode.CreateNodeConnector().node_uid = connectedUid;
        }
        private static IEnumerable<XElement> GetTypeEntitysFromAllEntitys(String typeName, IEnumerable<XElement> entitys)
        {
            return
                from entity in entitys
                where entity.Attribute("type").Value == typeName
                select entity;
        }
        private static IEnumerable<XElement> GetTypeFittingsFromAllFittings(IEnumerable<XElement> entitys_type, IEnumerable<XElement> fittings)
        {
            var uids = entitys_type.Select((elem) => elem.Attribute("UniqueId").Value);

            var existFittings = 
                from fitting in fittings
                let connectedUid = fitting.Attribute("ConnectorEntitys").Value.Split(";")
                where uids.Intersect(connectedUid).Count() > 0
                select fitting;

            //由于revitData出现了一条管道被7个附件连接的情况
            //而且三通和它的管件会重复连接管道
            //你是歌姬吧
            //existFittings=existFittings. <XElement>(new FittingConnectEqualityComparer());

            ISet<XElement> list = new HashSet<XElement>(new FittingConnectEqualityComparer());
            List<XElement> listAppend = new List<XElement>();
            foreach (XElement fitting in existFittings)
                if (fitting.Attribute("ConnectorEntitys").Value.Split(";").Length > 2)
                    list.Add(fitting);

            bool doDelete = false;
            foreach (XElement fitting in existFittings) 
            {
                var minConnectedUid = fitting.Attribute("ConnectorEntitys").Value.Split(";");
                if (minConnectedUid.Length <= 2)
                {
                    foreach (XElement maxFitting in list)
                    {
                        var maxConnectedUid = maxFitting.Attribute("ConnectorEntitys").Value.Split(";");

                        foreach (var minUid in minConnectedUid)
                        {
                            if (!maxConnectedUid.Contains(minUid))
                            {
                                break;
                            }
                            doDelete = true;
                        }
                        if (doDelete)
                            break;
                    }
                    if (!doDelete)
                        listAppend.Add(fitting);
                }
            }

            for (int i = 0; i < listAppend.Count(); i++)
                list.Add(listAppend[i]);

            //var set=existFittings
            //    .GroupBy(elem => 
            //    {

            //    } )
            //    .Select(group =>
            //    {
            //        group.OrderByDescending(elem => elem.Attribute("ConnectorEntitys").Value.Split(";").Length);
            //        return group.First();
            //    })
            //    .ToList();

            return list;
        }
    }
   
    class FittingConnectEqualityComparer : IEqualityComparer<XElement>
    {
        public bool Equals(XElement x, XElement y)
        {
            var xConnectedUid = x.Attribute("ConnectorEntitys").Value.Split(";");
            var yConnectedUid = y.Attribute("ConnectorEntitys").Value.Split(";");

            if (xConnectedUid.Length == yConnectedUid.Length)
            {
                foreach(var xUid in xConnectedUid) 
                {
                    if(!yConnectedUid.Contains(xUid))
                        return false;
                }
                return true;
            }
            else 
            {
                string[] maxConnectedUid;
                string[] minConnectedUid;
                if (xConnectedUid.Length > yConnectedUid.Length) 
                {
                    maxConnectedUid = xConnectedUid;
                    minConnectedUid = yConnectedUid;
                }
                else 
                {
                    maxConnectedUid = yConnectedUid;
                    minConnectedUid = xConnectedUid;
                }

                foreach (var minUid in minConnectedUid)
                {
                    if (!maxConnectedUid.Contains(minUid))
                        return false;
                }
                return true;
            }
        }

        public int GetHashCode([DisallowNull] XElement obj)
        {
            var list = obj.Attribute("ConnectorEntitys").Value.Split(";").ToList();
            list.Sort();
            return BitConverter.ToInt32(new MD5CryptoServiceProvider().ComputeHash(ASCIIEncoding.ASCII.GetBytes(String.Join('_', list))));
        }
    }

    public class NodeConnector
    {
        public string node_uid;
        public PipeNode node;
    }
    public class PipeNodeBase
    {
        public List<PipeNodeGroup> _pipeGroups = new List<PipeNodeGroup>();
    }
    public class PipeNodeGroup
    {
        public List<PipeNodeList> _pipeLists = new List<PipeNodeList>();
        public NodeType nodeType;
    }
    public class PipeNodeList
    {
        public List<PipeNode> _pipes = new List<PipeNode>();
        public PipeNodeList prev;
        public string teeId_next;
        public string teePoint_next;

        public string familyName;
        public string symbolName;
        public string systemClassfy;
        public string systemType;
        public string ductType;
        public string color;
        public string horizonOffset;
        public string verticalOffset;
    }

    public class PipeNode
    {
        public bool counted = false;
        public PipeNodeType pipeNodeType;

        public string uid;
        public string startPoint;
        public string endPoint;
        public string width;
        public string height;

        public NodeConnector[] connectors = new NodeConnector[4];

        private int firstNullIndex = 0;
        public int FirstNullIndex { get => firstNullIndex; private set => firstNullIndex = value; }
        public void ClearNodeConnector()
        {
            connectors = new NodeConnector[4];
            FirstNullIndex = 0;
            return;
        }
        public NodeConnector CreateNodeConnector()
        {
            if (FirstNullIndex >= 4)
                throw new Exception($"CreateNodeConnector:FirstNullIndex>=4,uid={this.uid}");
            return connectors[FirstNullIndex++] = new NodeConnector();
        }
    }

    public enum PipeNodeType
    {
        Unknow = 0,
        Single,
        Pipe,
        Bend,
        Tee,
        Four,
    }
    public enum NodeType
    {
        Unknow = 0,
        Pipe,
        Duct,
        Frame,
        Floor,
        Wall,
        Column,
        LinePipe,
        LineDuct,
    }
}
