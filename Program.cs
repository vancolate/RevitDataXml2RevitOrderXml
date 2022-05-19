using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using System.IO;

using static System.Console;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace RevitDataXml2RevitOrderXml
{
    class XmlFactory
    {
        static private int teeIndex=0;
        private int groupCount = 1;
         
        private XDocument Xdocument_Output;//= new XDocument();
        private XElement pipeBase_Output;// = new XElement("Root");
        private XElement tee_Output;// = new XElement("Root");
        public XmlFactory(XDocument readXDocument)
        {
            Xdocument_Output = readXDocument;
            //Xdocument_Output.Add(root_Output);
            pipeBase_Output = new XElement("PipeBase");
            Xdocument_Output.Root.Add(pipeBase_Output);
            tee_Output = new XElement("Tee");
            Xdocument_Output.Root.Add(tee_Output);
        }

        public void SaveXml(string savePath)
        {
            Xdocument_Output.Save(savePath);
        }

        public void AppendXml(PipeBase pipeNodeBase)
        {
            XElement pipeGroupXml;
            XElement pipeListXml;
            XElement pipeXml;

            foreach (var pipeGroup in pipeNodeBase._pipeGroups)
            {
                pipeGroupXml = new XElement("PipeGroup");
                int listCount = 1;
                //遍历组内所有管件列
                foreach (PipeList pipeList in pipeGroup._pipeLists)
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

                    //全部共用
                    pipeListXml.SetAttributeValue("PipeListNo", $"{listCount}");
                    pipeListXml.SetAttributeValue("PrevListNo", $"{pipeGroup._pipeLists.FindIndex((elem) => { return ReferenceEquals(elem, pipeList.prev); }) + 1}");
                    pipeListXml.SetAttributeValue("TeeId_Next", $"{pipeList.teeId_next}");

                    pipeListXml.SetAttributeValue("FamilyName", $"{pipeList.familyName}");
                    pipeListXml.SetAttributeValue("SymbolName", $"{pipeList.symbolName}");
                    pipeListXml.SetAttributeValue("SystemClassfy", $"{pipeList.systemClassfy}");
                    pipeListXml.SetAttributeValue("SystemType", $"{pipeList.systemType}");
                    pipeListXml.SetAttributeValue("HorizonOffset", $"{pipeList.horizonOffset}");
                    pipeListXml.SetAttributeValue("VerticalOffset", $"{pipeList.verticalOffset}");
                    pipeListXml.SetAttributeValue("Color", $"{pipeList.color}");

                    //仅Duct
                    if (pipeList.ductType != null)
                        pipeListXml.SetAttributeValue("DuctType", $"{pipeList.ductType}");
                    if (pipeGroup.nodeType == NodeType.LineDuct || pipeGroup.nodeType == NodeType.LinePipe)
                        pipeListXml.SetAttributeValue("Mark", $"{pipeList.mark?.Mark}");

                    pipeGroupXml.Add(pipeListXml);
                    //下一个管件列
                    listCount++;
                }
                pipeGroupXml.SetAttributeValue("PipeGroupNo", $"{groupCount}");
                pipeGroupXml.SetAttributeValue("Type", $"{pipeGroup.nodeType}");

                pipeBase_Output.Add(pipeGroupXml);
                //下一个管件组
                groupCount++;
            }
            return;
        }

        public void AppendTeeXml(PipeBase pipeNodeBase)
        {
            var dictionary = new Dictionary<int, Stack<int>>();
            //2.添加连接信息 Fittings✓ Entitys-InputConnector✘
            for (int pipeGroupNo = 0; pipeGroupNo < pipeNodeBase._pipeGroups.Count(); pipeGroupNo++)
            {
                var pipeGroup = pipeNodeBase._pipeGroups[pipeGroupNo];
                dictionary.Clear();
                for (int pipeListNo = 0; pipeListNo < pipeGroup._pipeLists.Count; pipeListNo++)
                {
                    //2.暂存列间
                    if (pipeListNo == 0) { }
                    else
                    {
                        var pipeList = pipeGroup._pipeLists[pipeListNo];
                        int index = pipeGroup._pipeLists.FindIndex((elem) => Object.ReferenceEquals(elem, pipeList.prev));
                        if (index == -1)
                            throw new Exception("FindIndex找不到");
                        if (dictionary.ContainsKey(index))
                        {
                            var stack = dictionary[index];
                            dictionary.Remove(index);
                            stack.Push(pipeListNo);
                            dictionary.Add(index, stack);
                        }
                        else
                        {
                            var stack = new Stack<int>();
                            stack.Push(pipeListNo);
                            dictionary.Add(index, stack);
                        }
                    }
                }

                //2.添加列间✓
                //至少是三通
                while (dictionary.Count > 0)
                {
                    var pair = dictionary.First();
                    dictionary.Remove(pair.Key);
                    int pipeListNo = pair.Key;
                    Stack<int> stack = pair.Value;

                    //线管全添加
                    tee_Output.Add(MakeTeeEntity(pipeGroup,pipeGroupNo, pipeListNo, stack));
                }
            }
            return;
        }
        private static XElement MakeTeeEntity(PipeGroup pipeGroup, int pipeGroupNo, int pipeListNo, Stack<int> otherPipeListNos)
        {
            //< Connector UniqueId = "22b0d132-700a-4311-99c1-a897f3091e1b-007334c3"
            //Group = "2"
            //Lists = "1,2,3"
            //ConnectorEntitys = "a34b07e8-2fe7-4e76-b514-b29b270c410c-007328d8;22b0d132-700a-4311-99c1-a897f3091e1b-007334b4" />
            //Point = "83893.656351341, 132177.063059417, 54899.999640338"

            XElement connector = new XElement("Connector");
            connector.SetAttributeValue("UniqueId", "Tee-"+teeIndex);
            connector.SetAttributeValue("Group", pipeGroupNo+1);

            StringBuilder Lists = new StringBuilder((pipeListNo + 1).ToString());
            var lists = pipeGroup._pipeLists[pipeListNo]._pipes;
            var startPipe = lists[lists.Count() - 1];
            StringBuilder ConnectorEntitys = new StringBuilder(startPipe.uid);

            List<float> x = new List<float>();
            List<float> y = new List<float>();
            List<float> z = new List<float>();
            { 
                var xyz = startPipe.endPoint.Split(',');
                x.Add(float.Parse(xyz[0]));
                y.Add(float.Parse(xyz[1]));
                z.Add(float.Parse(xyz[2]));
            }

            int resultPipeListNo;
            while (otherPipeListNos.TryPop(out resultPipeListNo))
            {
                Lists.Append($";{resultPipeListNo + 1}");
                Pipe pipe = pipeGroup._pipeLists[resultPipeListNo]._pipes[0];
                ConnectorEntitys.Append($";{pipe.uid}");

                var xyz = pipe.startPoint.Split(',');
                x.Add(float.Parse(xyz[0]));
                y.Add(float.Parse(xyz[1]));
                z.Add(float.Parse(xyz[2]));
            }
            connector.SetAttributeValue("Lists", Lists);
            connector.SetAttributeValue("ConnectorEntitys", ConnectorEntitys);
            connector.SetAttributeValue("Point", $"{x.Average()},{y.Average()},{z.Average()}");

            return connector;
        }
    }
    

    public class MainProgram
    {
        private static XmlFactory xmlFactory;
        private static int anonymousUid = 1;

        public MainProgram(string[] args) 
        {
            Main(args);
        }
        static void Main(string[] args)
        {
            //①找到xml
            string xmlReadPath = args[0];
            string xmlWritePath = null;
            WriteLine($"xml读取路径为:{xmlReadPath}");
            //WriteLine($"xml输出路径为:{xmlWritePath}");

            if (args.Length < 1)
            {
                WriteLine("缺少参数:xml读取路径");
                throw new ArgumentException("缺少参数:xml读取路径");
            }
            else if(args.Length > 1) 
            {
                xmlWritePath = args[1];
                WriteLine($"xml输出路径为:{xmlWritePath}");
            }
            else 
            {
                xmlWritePath = Path.Combine(Directory.GetCurrentDirectory(), "revit_order.xml");
                WriteLine($"xml默认输出路径为:{xmlWritePath}");
            }

            if (!File.Exists(xmlReadPath))
            {
                WriteLine("xml读取路径不正确");
                throw new ArgumentException("xml读取路径不正确");
            }

            //②读取xml
            WriteLine("载入xml中...");
            XDocument xDocument = XDocument.Load(xmlReadPath);
            xmlFactory = new XmlFactory(xDocument);

            var root_input = xDocument.Root;

            var _entitys = root_input.Element("Entitys").Elements("Entity");
            var fittings = root_input.Element("Fittings").Elements();
            var inputConnectors = root_input.Element("Entitys").Elements("InputConnector");


            //2.5 线管的读取uid 用来区分同类型的线管和管道
            var ac_line_uid =
                _entitys
                .Where(elem => elem.Attribute("type").Value == "AC Line")
                .Select(elem => elem.Attribute("UniqueId").Value);
            var dr_line_uid =
                _entitys
                .Where(elem => elem.Attribute("type").Value == "DR Line")
                .Select(elem => elem.Attribute("UniqueId").Value);


            //③+④+⑤
            var p1 = Process_AND_AppendXml_WithOneType(NodeType.Duct, _entitys, fittings, ac_line_uid);
            var p2 = Process_AND_AppendXml_WithOneType(NodeType.LineDuct, _entitys, inputConnectors, ac_line_uid);
            var p3 = Process_AND_AppendXml_WithOneType(NodeType.Pipe, _entitys, fittings, dr_line_uid);
            var p4 = Process_AND_AppendXml_WithOneType(NodeType.LinePipe, _entitys, inputConnectors, dr_line_uid);

            //额外⑥输出Tee到RevitOrder
            PipeBase pipeBase = new PipeBase() { _pipeGroups=p1._pipeGroups.Concat(p2._pipeGroups).Concat(p3._pipeGroups).Concat(p4._pipeGroups).ToList() };
            xmlFactory.AppendTeeXml(pipeBase);
            
            //⑤输出xml
            xmlFactory.SaveXml(xmlWritePath);

            WriteLine("程序结束");
            return;
        }

        private static PipeBase Process_AND_AppendXml_WithOneType(NodeType nodeType,IEnumerable<XElement> entitys, IEnumerable<XElement> fittings,IEnumerable<String> line_uids)
        {

            //(1)Duct (2)Pipe (3)LineDuct (4)LinePipe
            var entitys_type = GetTypeEntitysFromAllEntitys(nodeType, entitys, line_uids);
            var fittings_type = GetTypeFittingsFromAllFittings(entitys_type, fittings,nodeType);

            //③转化为自定义管道类
            //第一次获取连接数据,将xml化为内存对象.以及管道自己的数据.
            List<Pipe> OriginalPipes = GetPipeNodeFromDate(entitys_type, fittings_type, nodeType, entitys);

            //④处理管道类
            //去除弯头对外的连接,直连其两端
            DeleteFittingsNode(OriginalPipes, nodeType);

            //*******
            PipeBase pipeBase = Output2Base(OriginalPipes, nodeType);

            //第二次获取属性数据,从管道上获取,上放到列、组中
            foreach (var pipeGroup in pipeBase._pipeGroups)
            {
                {
                    var someonePipeXml = entitys_type.First(elem => elem.Attribute("UniqueId").Value == pipeGroup._pipeLists[0]._pipes[0].uid);
                    //在这里配置组拥有的属性
                    pipeGroup.nodeType = nodeType;//Enum.Parse<NodeType>(someonePipeXml.Attribute("type").Value);
                }

                foreach (var pipeList in pipeGroup._pipeLists)
                {
                    var someonePipeXml = entitys_type.First(elem => elem.Attribute("UniqueId").Value == pipeList._pipes[0].uid);
                    //在这里配置列拥有的属性

                    pipeList.familyName = someonePipeXml.Element("FamilyName").FirstAttribute.Value;
                    pipeList.symbolName = someonePipeXml.Element("SymbolName").FirstAttribute.Value;
                    pipeList.horizonOffset = someonePipeXml.Element("HorizonOffset").FirstAttribute.Value;
                    pipeList.verticalOffset = someonePipeXml.Element("VerticalOffset").FirstAttribute.Value;
                    pipeList.systemClassfy = someonePipeXml.Element("SystemClassfy").FirstAttribute.Value;
                    pipeList.systemType = someonePipeXml.Element("SystemType").FirstAttribute.Value;
                    pipeList.color = someonePipeXml.Element("Color").FirstAttribute.Value;

                    if (pipeGroup.nodeType == NodeType.Duct || pipeGroup.nodeType == NodeType.LineDuct)
                        pipeList.ductType = someonePipeXml.Element("DuctType").Attribute("value").Value;
                }
            }

            //⑤输出PipeBase到RevitOrder
            xmlFactory.AppendXml(pipeBase);

            return pipeBase;
        }




        //必须由管道开始遍历,且管道的一端为空或所连id为空,且管道不能有重复
        //调用Output2Group,加入pipeNodeBase中
        private static PipeBase Output2Base(List<Pipe> originalPipeNodes,NodeType nodeType)
        {
            PipeBase pipeNodeBase = new PipeBase();
            //遍历起点

            switch (nodeType)
            {
                case NodeType.Duct:
                case NodeType.Pipe:
                    int nullConnectorCount;
                    foreach (Pipe pipeNode in originalPipeNodes)
                    {
                        //必须由管道开始
                        //if (pipeNode.counted)
                        if (pipeNode.counted || pipeNode.pipeNodeType != PipeNodeType.Pipe)
                            continue;

                        nullConnectorCount = 0;
                        foreach (NodeConnector connector in pipeNode.connectors)
                        {
                            //找到一个起点: 存在至少3空
                            if (connector == null || connector.node == null)
                                nullConnectorCount++;
                        }
                        if (nullConnectorCount < 3)
                            continue;


                        PipeGroup pipeNodeGroup = new PipeGroup();
                        //顺序排入
                        Output2Group(pipeNode, pipeNodeGroup._pipeLists, nodeType);

                        //加入节点库
                        if (pipeNodeGroup._pipeLists.Count > 0)
                            pipeNodeBase._pipeGroups.Add(pipeNodeGroup);
                    }
                    break;
                case NodeType.LineDuct:
                case NodeType.LinePipe:
                    foreach (Pipe pipeNode in originalPipeNodes)
                    {
                        //必须由单头开始 ***待定:而且需要标记为S***
                        if (pipeNode.mark == null || pipeNode.mark.Mark != "S" || pipeNode.counted)//|| pipeNode.pipeNodeType != PipeNodeType.Single
                            continue;

                        PipeGroup pipeNodeGroup = new PipeGroup();
                        //顺序排入
                        Output2Group(pipeNode, pipeNodeGroup._pipeLists, nodeType);

                        //加入节点库
                        if (pipeNodeGroup._pipeLists.Count > 0)
                            pipeNodeBase._pipeGroups.Add(pipeNodeGroup);
                    }
                    break;
        }

            return pipeNodeBase;
        }

        private static void Output2Group(Pipe firstNode, List<PipeList> pipeNodeGroup, NodeType nodeType)//, Object first_AC_OR_DR = null
        {
            PipeList pipeNodeList = new PipeList();

            Pipe current;
            Pipe prev;
            PipeList prevList;
            //Object AC_OR_DR;
            //Stack<(PipeNode current, PipeNode prev, PipeNodeList prevList, Object AC_OR_DR)> nextStack = new Stack<(PipeNode, PipeNode, PipeNodeList, Object)>();
            Stack<(Pipe current, Pipe prev, PipeList prevList)> nextStack = new Stack<(Pipe, Pipe, PipeList)>();


            if (nodeType == NodeType.Pipe || nodeType == NodeType.Duct)
                nextStack.Push((firstNode, null, null));
            if (nodeType == NodeType.LinePipe || nodeType == NodeType.LineDuct)
            {
                //跳过开头的单头
                foreach (NodeConnector connector in firstNode.connectors)
                {
                    if (connector == null || connector.node == null)
                        continue;
                    nextStack.Push((connector.node, firstNode, null));
                    break;
                }
                pipeNodeList.mark = new MarkInput() { Mark = "S" };
            }


            do
            { 
                //拿一个
                (current, prev, prevList) = nextStack.Pop();

                //判断是否重复了
                if (current.counted)
                {
                    if (pipeNodeList._pipes.Count > 0)
                    {
                        pipeNodeList.prev = prevList;
                        if (pipeNodeList._pipes.Count > 0)
                            pipeNodeGroup.Add(pipeNodeList);
                        pipeNodeList = new PipeList() { };
                    }
                    continue;
                }
                current.counted = true;


                //判断管件类型
                switch (current.pipeNodeType)
                {
                    case PipeNodeType.Pipe:
                    //判断是管道
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
                            //如果没有连接,说明到终点了,当前列加入组,重新开个列
                            if (!connected)
                            {
                                //加入
                                pipeNodeList.prev = prevList;
                                if (pipeNodeList._pipes.Count > 0)
                                    pipeNodeGroup.Add(pipeNodeList);
                                pipeNodeList = new PipeList();
                            }
                        }
                        break;
                    case PipeNodeType.Tee:
                    case PipeNodeType.Four:
                        {
                            //判断是三通四通
                            //加入
                            pipeNodeList.prev = prevList;
                            pipeNodeList.teeId_next = current.uid;
                            //pipeNodeList.teePoint_next = current.startPoint;
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
                            pipeNodeList = new PipeList();
                        }
                        break;

                    case PipeNodeType.Single:
                        //判断是单头(结束时)(线管专用)
                        {
                            switch (nodeType)
                            {
                                case NodeType.LinePipe:
                                case NodeType.LineDuct:
                                    if( current.mark.Mark=="S")
                                        pipeNodeList.mark = new MarkInput() { Mark = "S" };
                                    else
                                        pipeNodeList.mark = new MarkInput() { };
                                    break;
                                default:
                                    throw new Exception($"组排序单头:类型错误:{nodeType}.current.uid={current.uid}");
                            }
                            pipeNodeList.prev = prevList;
                            if (pipeNodeList._pipes.Count > 0)
                                pipeNodeGroup.Add(pipeNodeList);
                            pipeNodeList = new PipeList();
                        }
                        break;
                    default:
                        throw new Exception($"current.pipeNodeType值异常,current.uid={current.uid}");
                }
            } while (nextStack.Count > 0);

        }
        private static void DeleteFittingsNode(List<Pipe> originalPipeNodes,NodeType nodeType)
        {
            foreach (Pipe pipeNode in originalPipeNodes)
            {
                //(可能)是弯头,才能去除弯头
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

                    foreach (NodeConnector first_Connector in firstConnector.node.connectors)
                    {
                        if (first_Connector == null)
                            continue;
                        if (Object.ReferenceEquals(pipeNode, first_Connector.node))
                        {
                            //来了来了
                            first_Connector.node = null;
                            first_Connector.node_uid = String.Empty;
                            break;
                        }
                    }
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

                    foreach (NodeConnector first_Connector in firstConnector.node.connectors)
                    {
                        if (first_Connector == null)
                            continue;
                        if (Object.ReferenceEquals(pipeNode, first_Connector.node))
                        {
                            //来了来了
                            first_Connector.node = secondConnector.node;
                            first_Connector.node_uid = secondConnector.node_uid;
                            break;
                        }
                    }
                    foreach (NodeConnector second_Connector in secondConnector.node.connectors)
                    {
                        if (second_Connector == null)
                            continue;
                        if (Object.ReferenceEquals(pipeNode, second_Connector.node))
                        {
                            //来了来了
                            second_Connector.node = firstConnector.node;
                            second_Connector.node_uid = firstConnector.node_uid;
                            break;
                        }
                    }
                    //if (firstGetConnector == null || secondGetConnector == null)
                    //    throw new Exception("我能连到你,你却连不到我??");
                }
            }
        }

        //由entitys_duct提出管道,由fittings_duct提出管件,组成包含所有元素的列,然后在里面调用id连接,使自定义管道实例化完成
        private static List<Pipe> GetPipeNodeFromDate(IEnumerable<XElement> entitys_type, IEnumerable<XElement> fittings_type, NodeType nodeType, IEnumerable<XElement> entitys)
        {
            List<Pipe> OriginalPipeNodes = new List<Pipe>();

            foreach (var entity in entitys_type)
            {
                //为当前管道xml新建管道实例
                Pipe pipeNode = new Pipe()
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
                    case NodeType.LineDuct:
                        if (entity.Element("DuctType").Attribute("value").Value == "圆形")
                            pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("DuctType").Attribute("Length1").Value) * 2).ToString();
                        else
                        {
                            pipeNode.width = entity.Element("DuctType").Attribute("Length1").Value;
                            pipeNode.height = entity.Element("DuctType").Attribute("Length2").Value;
                        }
                        break;
                    case NodeType.Pipe:
                    case NodeType.LinePipe:
                        pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("Radius").FirstAttribute.Value) * 2).ToString();
                        break;
                    //线管变真管
                }

                //加入数组
                OriginalPipeNodes.Add(pipeNode);
            }

            //连接件
            foreach (var fitting in fittings_type)
            {
                Pipe pipeNode;
                switch (nodeType)
                {
                    case NodeType.Duct:
                    case NodeType.Pipe:
                            pipeNode = new Pipe()
                            {
                                uid = fitting.Attribute("UniqueId").Value,
                                //startPoint = fitting.Attribute("Point").Value,
                            };
                        break;

                    case NodeType.LineDuct:
                    case NodeType.LinePipe:
                        pipeNode = new Pipe()
                        {
                            //即开头 非开头又有uid的是错误使用案例
                            uid = fitting.Attribute("UniqueId").Value,
                        };
                        //不是单头
                        if (pipeNode.uid == null || pipeNode.uid == String.Empty)
                            pipeNode.uid = (anonymousUid++).ToString();
                        //是单头
                        else 
                        {
                            XElement entityXml=entitys.First(elem=> { return elem.Attribute("UniqueId").Value== pipeNode.uid; });
                            pipeNode.mark = new MarkInput() { Mark= entityXml.Element("Params").Attribute("Mark").Value };
                        }

                        break;
                    //线管变真管
                    default:
                        throw new Exception("非管类型");
                }

                //连接件类型
                var connectedUids = fitting.Attribute("ConnectorEntitys").Value.Trim(';').Split(";");
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

        private static Pipe FindId(string uid, List<Pipe> pipeNodes)
        {
            Pipe pipeNode = pipeNodes.FirstOrDefault((node) => { return node.uid == uid; });
            if (pipeNode != null)
                return pipeNode;
            else
                return null;
        }
        private static void ConnectPipeNode(List<Pipe> pipeNodes)
        {
            foreach (Pipe pipeNode in pipeNodes)
            {
                for (int i = 0; i < pipeNode.FirstNullIndex; i++)
                    if (pipeNode.connectors[i].node_uid != String.Empty)
                    {
                        //正向连接
                        Pipe neighbour = FindId(pipeNode.connectors[i].node_uid, pipeNodes);

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

        private static void ConnectPipeNodeId(string[] connectedUids, Pipe pipeNode)
        {
            //连接节点
            foreach (string connectedUid in connectedUids)
                //变得超简单
                pipeNode.CreateNodeConnector().node_uid = connectedUid;
        }
        private static IEnumerable<XElement> GetTypeEntitysFromAllEntitys(NodeType nodeType, IEnumerable<XElement> entitys, IEnumerable<String> line_uids)
        {
            switch (nodeType)
            {
                case NodeType.Duct:
                    return
                        from entity in entitys
                        where entity.Attribute("type").Value == "Duct"
                        where !line_uids.Contains(entity.Attribute("UniqueId").Value)
                        select entity;

                case NodeType.LineDuct:
                    return
                        from entity in entitys
                        where entity.Attribute("type").Value == "Duct"
                        where line_uids.Contains(entity.Attribute("UniqueId").Value) //entity.Element("FamilyName").FirstAttribute.Value == "AC Line"
                        select entity;

                case NodeType.Pipe:
                    return
                        from entity in entitys
                        where entity.Attribute("type").Value == "Pipe"
                        where !line_uids.Contains(entity.Attribute("UniqueId").Value)
                        select entity;

                case NodeType.LinePipe:
                    return
                        from entity in entitys
                        where entity.Attribute("type").Value == "Pipe"
                        where line_uids.Contains(entity.Attribute("UniqueId").Value) //entity.Element("FamilyName").FirstAttribute.Value == "DR Line"
                        select entity;

                default:
                    throw new Exception("非管类型");
            }
        }
        private static IEnumerable<XElement> GetTypeFittingsFromAllFittings(IEnumerable<XElement> entitys_type, IEnumerable<XElement> fittings,NodeType nodeType)
        {
            //拿到所有管道uid
            var uids = entitys_type.Select((elem) => elem.Attribute("UniqueId").Value);
            //由管道uid选出所有这种类型的管件
            var existFittings = 
                from fitting in fittings
                let connectedUid = fitting.Attribute("ConnectorEntitys").Value.Trim(';').Split(";")
                where uids.Intersect(connectedUid).Count() > 0
                select fitting;

            //线每个管件,都是需要的
            if (nodeType == NodeType.LineDuct || nodeType == NodeType.LinePipe)
                return existFittings;


            //管每个管件是软件组自己创的
            //使得revitData出现了一条管道被7个附件连接的情况
            //而且三通和它的管件会重复连接管道
            //歌姬吧
            ISet<XElement> list = new HashSet<XElement>(new FittingConnectEqualityComparer());
            List<XElement> listAppend = new List<XElement>();
            List<XElement> removeList = new List<XElement>();
            foreach (XElement fitting in existFittings) 
            {
                var tempUids = fitting.Attribute("ConnectorEntitys").Value.Trim(';').Split(";");
                if (tempUids.Length > 2)
                    list.Add(fitting);
                //更新:寄摆 管道圆环连接信息输出又变了
                //现在如果是三/四通的管件,圆环的两个连接居然是相同的
                //直接去除
                else if (tempUids.Length == 2 && tempUids[0] == tempUids[1])
                    removeList.Add(fitting);
            }
            existFittings = existFittings.Except(removeList);

            foreach (XElement fitting in existFittings) 
            {
                bool doDelete = false;
                var minConnectedUid = fitting.Attribute("ConnectorEntitys").Value.Trim(';').Split(";");
                int minLength = minConnectedUid.Length;
                if (minLength == 2)
                {
                    foreach (XElement maxFitting in list)
                    {
                        var maxConnectedUid = maxFitting.Attribute("ConnectorEntitys").Value.Trim(';').Split(";");

                        if (maxConnectedUid.Intersect(minConnectedUid).Count() == minLength) 
                        {
                            doDelete = true;
                            break;
                        }
                    }
                    if (!doDelete)
                        listAppend.Add(fitting);
                }
            }

            for (int i = 0; i < listAppend.Count(); i++)
                list.Add(listAppend[i]);


            return list;
        }
    }

    
    class FittingConnectEqualityComparer : IEqualityComparer<XElement>
    {
        public bool Equals(XElement x, XElement y)
        {
            var xConnectedUid = x.Attribute("ConnectorEntitys").Value.Trim(';').Split(";");
            var yConnectedUid = y.Attribute("ConnectorEntitys").Value.Trim(';').Split(";");

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
            var list = obj.Attribute("ConnectorEntitys").Value.Trim(';').Split(";").ToList();
            list.Sort();
            return BitConverter.ToInt32(new MD5CryptoServiceProvider().ComputeHash(ASCIIEncoding.ASCII.GetBytes(String.Join('_', list))));
        }
    }
   
    public class NodeConnector
    {
        public string node_uid;
        public Pipe node;
    }
    public class PipeBase
    {
        public List<PipeGroup> _pipeGroups = new List<PipeGroup>();
    }
    public class PipeGroup
    {
        public List<PipeList> _pipeLists = new List<PipeList>();
        public NodeType nodeType;
    }
    public class PipeList
    {
        public List<Pipe> _pipes = new List<Pipe>();
        public PipeList prev;
        public string teeId_next;
        //public string teePoint_next;

        public string familyName;
        public string symbolName;
        public string systemClassfy;
        public string systemType;
        public string ductType;
        public string color;
        public string horizonOffset;
        public string verticalOffset;

        //public ACLine ac;
        //public DRLine dr;
        public MarkInput mark;
    }

    public class Pipe
    {
        public bool counted = false;

        public PipeNodeType pipeNodeType;
        public string uid;
        public string startPoint;
        public string endPoint;
        public string width;
        public string height;

        public MarkInput mark = null;

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
    //public class ACLine
    //{
    //    public string SquareRound = null;
    //    public string ClosedDuct = null;
    //    public string Size = null;
    //    public string SystemType = null;
    //    public string PipeJoint = null;

    //    public string InstallationSpace = null;
    //    public string InsulationThickness = null;
    //    public string PriorityandSpecial = null;
    //    public string GoThroughWall = null;
    //    public string GoThroughBeam = null;
    //}

    //public class DRLine
    //{
    //    public string Diameter = null;
    //    public string SystemType = null;
    //    public string PipeMaterial = null;
    //    public string PipeJoint = null;
    //    public string TrapCleanout = null;

    //    public string Bend4590 = null;
    //    public string InstallationSpace = null;
    //    public string InsulationThickness = null;
    //    public string Slope = null;
    //    public string Priority = null;

    //    public string GoThroughWall = null;
    //    public string GoThroughBeam = null;
    //}
    public class MarkInput
    {
        public string Mark = null;
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
