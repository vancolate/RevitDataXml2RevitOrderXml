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
        private static int groupCount = 1;

        private static XDocument Xdocument_Output = new XDocument();
        private static XElement root_Output = new XElement("Root");
        static XmlFactory()
        {
            Xdocument_Output.Add(root_Output);
        }

        public static void SaveXml(string savePath)
        {
            Xdocument_Output.Save(savePath);
        }

        public static void AppendXml(PipeNodeBase pipeNodeBase)
        {
            XElement pipeGroupXml;
            XElement pipeListXml;
            XElement pipeXml;

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

                    //全部共用
                    pipeListXml.SetAttributeValue("PipeListNo", $"{listCount}");
                    pipeListXml.SetAttributeValue("PrevListNo", $"{pipeGroup._pipeLists.FindIndex((elem) => { return ReferenceEquals(elem, pipeList.prev); }) + 1}");
                    pipeListXml.SetAttributeValue("TeeId_Next", $"{pipeList.teeId_next}");
                    pipeListXml.SetAttributeValue("TeePoint_Next", $"{pipeList.teePoint_next}");

                    //Pipe与Duct共用
                    if (pipeList.familyName != null)
                    {
                        pipeListXml.SetAttributeValue("FamilyName", $"{pipeList.familyName}");
                        pipeListXml.SetAttributeValue("SymbolName", $"{pipeList.symbolName}");
                        pipeListXml.SetAttributeValue("SystemClassfy", $"{pipeList.systemClassfy}");
                        pipeListXml.SetAttributeValue("SystemType", $"{pipeList.systemType}");
                        pipeListXml.SetAttributeValue("HorizonOffset", $"{pipeList.horizonOffset}");
                        pipeListXml.SetAttributeValue("VerticalOffset", $"{pipeList.verticalOffset}");
                        pipeListXml.SetAttributeValue("Color", $"{pipeList.color}");
                    }

                    //仅Duct
                    if (pipeList.ductType != null)
                        pipeListXml.SetAttributeValue("DuctType", $"{pipeList.ductType}");
                    //仅AC/仅DR
                    else if (pipeList.ac != null)
                    {
                        pipeListXml.SetAttributeValue("SquareORRound", $"{pipeList.ac.SquareORRound}");
                        pipeListXml.SetAttributeValue("ClosedDuct", $"{pipeList.ac.ClosedDuct}");
                        pipeListXml.SetAttributeValue("Size", $"{pipeList.ac.Size}");
                        pipeListXml.SetAttributeValue("SDRSystemType", $"{pipeList.ac.SDRSystemType}");
                        pipeListXml.SetAttributeValue("PipeJoint", $"{pipeList.ac.PipeJoint}");

                        pipeListXml.SetAttributeValue("InstallationSpace", $"{pipeList.ac.InstallationSpace}");
                        pipeListXml.SetAttributeValue("InsulationThickness", $"{pipeList.ac.InsulationThickness}");
                        pipeListXml.SetAttributeValue("PriorityANDSpecial", $"{pipeList.ac.PriorityANDSpecial}");
                        pipeListXml.SetAttributeValue("GoThroughWallORBeam", $"{pipeList.ac.GoThroughWallORBeam}");
                    }
                    else if (pipeList.dr != null)
                    {
                        pipeListXml.SetAttributeValue("DiameterDN", $"{pipeList.dr.DiameterDN}");
                        pipeListXml.SetAttributeValue("SDRSystemType", $"{pipeList.dr.SDRSystemType}");
                        pipeListXml.SetAttributeValue("PipeMaterial", $"{pipeList.dr.PipeMaterial}");
                        pipeListXml.SetAttributeValue("PipeJoint", $"{pipeList.dr.PipeJoint}");
                        pipeListXml.SetAttributeValue("TrapORCleanout", $"{pipeList.dr.TrapORCleanout}");

                        pipeListXml.SetAttributeValue("Bend45ORBend90", $"{pipeList.dr.Bend45ORBend90}");
                        pipeListXml.SetAttributeValue("InstallationSpace", $"{pipeList.dr.InstallationSpace}");
                        pipeListXml.SetAttributeValue("InsulationThickness", $"{pipeList.dr.InsulationThickness}");
                        pipeListXml.SetAttributeValue("Slope", $"{pipeList.dr.Slope}");
                        pipeListXml.SetAttributeValue("Priority", $"{pipeList.dr.Priority}");

                        pipeListXml.SetAttributeValue("GoThroughWallORBeam", $"{pipeList.dr.GoThroughWallORBeam}");
                    }

                    pipeGroupXml.Add(pipeListXml);
                    //下一个管件列
                    listCount++;
                }
                pipeGroupXml.SetAttributeValue("PipeGroupNo", $"{groupCount}");
                pipeGroupXml.SetAttributeValue("Type", $"{pipeGroup.nodeType}");

                root_Output.Add(pipeGroupXml);
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
            var root_input = xDocument.Root;

            var entitys = root_input.Element("Entitys").Elements();
            var fittings = root_input.Element("Fittings").Elements();

            //③+④+⑤
            Process_AND_AppendXml_WithOneType(NodeType.Pipe, entitys, fittings);
            Process_AND_AppendXml_WithOneType(NodeType.Duct, entitys, fittings);
            Process_AND_AppendXml_WithOneType(NodeType.LinePipe, entitys, fittings);
            Process_AND_AppendXml_WithOneType(NodeType.LineDuct, entitys, fittings);

            //⑤输出xml
            XmlFactory.SaveXml(xmlWritePath);

            WriteLine("程序结束");
            return;
        }

        private static void Process_AND_AppendXml_WithOneType(NodeType nodeType,IEnumerable<XElement> entitys, IEnumerable<XElement> fittings)
        {

            //(1)Duct✓ (2)Pipe (3)LineDuct (4)LinePipe
            var entitys_type = GetTypeEntitysFromAllEntitys(nodeType.ToString(), entitys);
            var fittings_type = GetTypeFittingsFromAllFittings(entitys_type, fittings,nodeType);

            //③转化为自定义管道类
            //第一次获取连接数据,将xml化为内存对象.以及管道自己的数据.
            List<PipeNode> OriginalPipeNodes = GetPipeNodeFromDate(entitys_type, fittings_type, nodeType);

            //④处理管道类
            //去除弯头对外的连接,直连其两端
            DeleteFittingsNode(OriginalPipeNodes, nodeType);

            PipeNodeBase pipeNodeBase = Output2Base(OriginalPipeNodes, nodeType);

            //第二次获取属性数据,从管道上获取,上放到列、组中
            foreach (var pipeGroup in pipeNodeBase._pipeGroups)
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

                    switch (nodeType)
                    {
                        case NodeType.Pipe:
                        case NodeType.Duct:
                            pipeList.familyName = someonePipeXml.Element("FamilyName").FirstAttribute.Value;
                            pipeList.symbolName = someonePipeXml.Element("SymbolName").FirstAttribute.Value;
                            pipeList.horizonOffset = someonePipeXml.Element("HorizonOffset").FirstAttribute.Value;
                            pipeList.verticalOffset = someonePipeXml.Element("VerticalOffset").FirstAttribute.Value;
                            pipeList.systemClassfy = someonePipeXml.Element("SystemClassfy").FirstAttribute.Value;
                            pipeList.systemType = someonePipeXml.Element("SystemType").FirstAttribute.Value;
                            pipeList.color = someonePipeXml.Element("Color").FirstAttribute.Value;
                            break;
                        case NodeType.LinePipe:
                        Error: 上放至管道的DR属性;
                            //pipeList.dr = 
                            break;
                        case NodeType.LineDuct:
                        Error: 上放至管道的AC属性;
                            //pipeList.ac=
                            break;
                        default:
                            throw new Exception("只支持风/水管,线风/水管类型.");
                    }

                    if (pipeGroup.nodeType == NodeType.Duct)
                        pipeList.ductType = someonePipeXml.Element("DuctType").Attribute("value").Value;
                }
            }

            //⑤输出RevitOrder.xml
            XmlFactory.AppendXml(pipeNodeBase);
        }




        //必须由管道开始遍历,且管道的一端为空或所连id为空,且管道不能有重复
        //调用Output2Group,加入pipeNodeBase中
        private static PipeNodeBase Output2Base(List<PipeNode> originalPipeNodes,NodeType nodeType)
        {
            PipeNodeBase pipeNodeBase = new PipeNodeBase();
            //遍历起点

            switch (nodeType)
            {
                case NodeType.Pipe:
                case NodeType.Duct:
                    int nullConnectorCount;
                    foreach (PipeNode pipeNode in originalPipeNodes)
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


                        PipeNodeGroup pipeNodeGroup = new PipeNodeGroup();
                        //顺序排入
                        Output2Group(pipeNode, pipeNodeGroup._pipeLists, nodeType);

                        //加入节点库
                        if (pipeNodeGroup._pipeLists.Count > 0)
                            pipeNodeBase._pipeGroups.Add(pipeNodeGroup);
                    }
                    break;
                case NodeType.LineDuct:
                    foreach (PipeNode pipeNode in originalPipeNodes)
                    {
                        //必须由单头开始 ***待定:而且需要标记为S***
                        if (pipeNode.mark==null || pipeNode.mark.Mark != "S" || pipeNode.counted)//|| pipeNode.pipeNodeType != PipeNodeType.Single
                            continue;

                        PipeNodeGroup pipeNodeGroup = new PipeNodeGroup();
                        //顺序排入
                        Output2Group(pipeNode, pipeNodeGroup._pipeLists, nodeType);

                        //加入节点库
                        if (pipeNodeGroup._pipeLists.Count > 0)
                            pipeNodeBase._pipeGroups.Add(pipeNodeGroup);
                    }
                    break;
                case NodeType.LinePipe:
                    foreach (PipeNode pipeNode in originalPipeNodes)
                    {
                        //必须由单头开始 ***待定:而且需要标记为S***
                        if (pipeNode.mark == null || pipeNode.mark.Mark != "S" || pipeNode.counted)//|| pipeNode.pipeNodeType != PipeNodeType.Single
                            continue;

                        PipeNodeGroup pipeNodeGroup = new PipeNodeGroup();
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

        private static void Output2Group(PipeNode firstNode, List<PipeNodeList> pipeNodeGroup, NodeType nodeType)//, Object first_AC_OR_DR = null
        {
            PipeNodeList pipeNodeList = new PipeNodeList();

            PipeNode current;
            PipeNode prev;
            PipeNodeList prevList;
            //Object AC_OR_DR;
            //Stack<(PipeNode current, PipeNode prev, PipeNodeList prevList, Object AC_OR_DR)> nextStack = new Stack<(PipeNode, PipeNode, PipeNodeList, Object)>();
            Stack<(PipeNode current, PipeNode prev, PipeNodeList prevList)> nextStack = new Stack<(PipeNode, PipeNode, PipeNodeList)>();

            ////线管开头管件的SDR
            ////糟糕的代码
            //if (first_AC_OR_DR != null)
            //{
            //    if (nodeType == NodeType.LineDuct)
            //        pipeNodeList.ac = (first_AC_OR_DR as ACLine);
            //    else if(nodeType == NodeType.LinePipe)
            //        pipeNodeList.dr = (first_AC_OR_DR as DRLine);
            //    else
            //        throw new Exception("存在参数first_AC_OR_DR但不是线管");

            //    //跳过开头的单头
            //    foreach (NodeConnector connector in firstNode.connectors)
            //    {
            //        if (connector == null || connector.node == null)
            //            continue;
            //        nextStack.Push((connector.node, firstNode, null, null));
            //        break;
            //    }
            //}
            //else 
            //{
            //    nextStack.Push((firstNode, null, null, null));
            //}

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
                        pipeNodeList = new PipeNodeList() { };
                    }
                    continue;
                }
                current.counted = true;
                //WPFConsole($"{current.id}");

                ////糟糕的代码2
                //if(AC_OR_DR != null) 
                //{
                //    if (nodeType == NodeType.LineDuct)
                //        pipeNodeList.ac = (AC_OR_DR as ACLine);
                //    else if (nodeType == NodeType.LinePipe)
                //        pipeNodeList.dr = (AC_OR_DR as DRLine);
                //    else
                //        throw new Exception("存在参数AC_OR_DR但不是线管");
                //}

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
                                pipeNodeList = new PipeNodeList();
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
                            pipeNodeList.teePoint_next = current.startPoint;
                            if (pipeNodeList._pipes.Count > 0)
                                pipeNodeGroup.Add(pipeNodeList);
                            //分支循环
                            foreach (NodeConnector connector in current.connectors)
                            {
                                if (connector == null || connector.node == null || connector.node == prev)
                                    continue;

                                ////真管,线管判断
                                //if(nodeType == NodeType.LinePipe || nodeType == NodeType.LineDuct) 
                                //{
                                //    Error;无需上放
                                //    if (current.ac != null)
                                //        nextStack.Push((connector.node, current, pipeNodeList, current.ac));
                                //    else if (current.dr != null)
                                //        nextStack.Push((connector.node, current, pipeNodeList, current.dr));
                                //    else
                                //        nextStack.Push((connector.node, current, pipeNodeList, null));
                                //}
                                //else 
                                //{
                                //    nextStack.Push((connector.node, current, pipeNodeList, null));
                                //}

                                nextStack.Push((connector.node, current, pipeNodeList));
                                continue;
                            }
                            pipeNodeList = new PipeNodeList();
                        }
                        break;
                    //case PipeNodeType.Bend:
                    //    //判断是双通(线管专用)
                    //    {
                    //        if (nodeType != NodeType.LineDuct && nodeType != NodeType.LinePipe)
                    //            throw new Exception("只有线管双通可以在删除弯头后存活");
                    //        //加入
                    //        pipeNodeList.prev = prevList;
                    //        //pipeNodeList.teeId_next = current.uid;
                    //        //pipeNodeList.teePoint_next = current.startPoint;
                    //        if (pipeNodeList._pipes.Count > 0)
                    //            pipeNodeGroup.Add(pipeNodeList);
                    //        //分支循环
                    //        foreach (NodeConnector connector in current.connectors)
                    //        {
                    //            if (connector == null || connector.node == null || connector.node == prev)
                    //                continue;

                    //            //若是真SDR,新开一个列,并传递SDR
                    //            if (current.ac != null)
                    //            {
                    //                nextStack.Push((connector.node, current, pipeNodeList, current.ac));
                    //            }
                    //            else if (current.dr != null)
                    //            {
                    //                nextStack.Push((connector.node, current, pipeNodeList, current.dr));
                    //            }
                    //            else
                    //                throw new Exception("已经去除了无价值弯头,怎么还有空SDR的弯头管件?");
                    //            continue;
                    //        }
                    //        pipeNodeList = new PipeNodeList();
                    //    }
                    //    break;
                    case PipeNodeType.Single:
                        //判断是单头(结束时)(线管专用)
                        {
                            switch (nodeType)
                            {
                                case NodeType.Pipe:
                                case NodeType.Duct:
                                    //不用管
                                    continue;
                                case NodeType.LinePipe:
                                case NodeType.LineDuct:
                                    ////若是真SDR且为开头,覆盖列的SDR,结束
                                    ////***线管管件被标记为S,即开头时 * **
                                    //if (current.ac != null && current.ac.Mark == "S")
                                    //    pipeNodeList.ac = current.ac;
                                    //else if (current.dr != null && current.dr.Mark == "S")
                                    //    pipeNodeList.dr = current.dr;
                                    ////else
                                    //    //throw new Exception("线管单头管件但是它没有ac/dr");
                                    break;
                            }
                            pipeNodeList.prev = prevList;
                            if (pipeNodeList._pipes.Count > 0)
                                pipeNodeGroup.Add(pipeNodeList);
                            pipeNodeList = new PipeNodeList();
                        }
                        break;
                    default:
                        throw new Exception("current.pipeNodeType == ?");
                }
            } while (nextStack.Count > 0);

        }
        private static void DeleteFittingsNode(List<PipeNode> originalPipeNodes,NodeType nodeType)
        {
            foreach (PipeNode pipeNode in originalPipeNodes)
            {
                //(可能)是弯头,才能去除弯头
                if (pipeNode.pipeNodeType != PipeNodeType.Bend)
                    continue;

                ////线管无价值弯头去除
                //switch (nodeType)
                //{
                //    case NodeType.Pipe:
                //    case NodeType.Duct:
                //        break;
                //    case NodeType.LinePipe:
                //        if (pipeNode.dr != null)
                //            continue;
                //        break;
                //    case NodeType.LineDuct:
                //        if (pipeNode.ac != null)
                //            continue;
                //        break;
                //    default:
                //        throw new Exception("不是管??");
                //}

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
        private static List<PipeNode> GetPipeNodeFromDate(IEnumerable<XElement> entitys_type, IEnumerable<XElement> fittings_type, NodeType nodeType)
        {
            List<PipeNode> OriginalPipeNodes = new List<PipeNode>();

            foreach (var entity in entitys_type)
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
                            pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("DuctType").Attribute("Length1").Value) * 2).ToString();
                        else
                        {
                            pipeNode.width = entity.Element("DuctType").Attribute("Length1").Value;
                            pipeNode.height = entity.Element("DuctType").Attribute("Length2").Value;
                        }
                        break;
                    case NodeType.Pipe:
                        pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("Radius").FirstAttribute.Value) * 2).ToString();
                        break;
                    //线管不需要配置宽高,线管件下面的循环配置可能存在的SDR
                    case NodeType.LineDuct:
                    Error: 线管利用ac设置尺寸;
                        //if (entity.Element("DuctType").Attribute("value").Value == "圆形")
                        //    pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("DuctType").Attribute("Length1").Value) * 2).ToString();
                        //else
                        //{
                        //    pipeNode.width = entity.Element("DuctType").Attribute("Length1").Value;
                        //    pipeNode.height = entity.Element("DuctType").Attribute("Length2").Value;
                        //}

                        //pipeNode.ac = new ACLine()
                        //{
                        //    SquareORRound = fitting.Attribute("SquareORRound")?.Value,
                        //    ClosedDuct = fitting.Attribute("ClosedDuct")?.Value,
                        //    Size = fitting.Attribute("Size")?.Value,
                        //    SDRSystemType = fitting.Attribute("SDRSystemType")?.Value,
                        //    PipeJoint = fitting.Attribute("PipeJoint")?.Value,

                        //    InstallationSpace =fitting.Attribute("InstallationSpace")?.Value,
                        //    InsulationThickness =fitting.Attribute("InsulationThickness")?.Value,
                        //    PriorityANDSpecial =fitting.Attribute("PriorityANDSpecial")?.Value,
                        //    GoThroughWallORBeam =fitting.Attribute("GoThroughWallORBeam")?.Value,
                        //};
                        break;
                    case NodeType.LinePipe:
                    Error: 线管利用dr设置尺寸;
                        //pipeNode.width = pipeNode.height = (Int32.Parse(entity.Element("Radius").FirstAttribute.Value) * 2).ToString();

                        //pipeNode.dr = new DRLine()
                        //{
                        //    DiameterDN = fitting.Attribute("DiameterDN")?.Value,
                        //    SDRSystemType = fitting.Attribute("SDRSystemType")?.Value,
                        //    PipeMaterial =fitting.Attribute("PipeMaterial")?.Value,
                        //    PipeJoint = fitting.Attribute("PipeJoint")?.Value,
                        //    TrapORCleanout = fitting.Attribute("TrapORCleanout")?.Value,

                        //    Bend45ORBend90 = fitting.Attribute("Bend45ORBend90")?.Value,
                        //    InstallationSpace =fitting.Attribute("InstallationSpace")?.Value,
                        //    InsulationThickness =fitting.Attribute("InsulationThickness")?.Value,
                        //    Slope = fitting.Attribute("Slope")?.Value,
                        //    Priority = fitting.Attribute("Priority")?.Value,

                        //    GoThroughWallORBeam =fitting.Attribute("GoThroughWallORBeam")?.Value,
                        //};
                        break;
                }

                //加入数组
                OriginalPipeNodes.Add(pipeNode);
            }

            //连接件
            foreach (var fitting in fittings_type)
            {
                PipeNode pipeNode = new PipeNode()
                {
                    uid = fitting.Attribute("UniqueId").Value,
                    startPoint= fitting.Attribute("Point").Value,
                };

                //"Mark" 是AC和DR的MarkInput点共有的属性
                if (nodeType==NodeType.LineDuct || nodeType == NodeType.LinePipe)
                {
                Error: 获取mark属性 ? "S";
                    pipeNode.mark = new MarkInput()
                    {
                        Mark= fitting.Attribute("Mark")?.Value,
                    };
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
        private static IEnumerable<XElement> GetTypeFittingsFromAllFittings(IEnumerable<XElement> entitys_type, IEnumerable<XElement> fittings,NodeType nodeType)
        {
            var uids = entitys_type.Select((elem) => elem.Attribute("UniqueId").Value);

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

        public ACLine ac;
        public DRLine dr;
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
    public class ACLine
    {
        public string SquareORRound = null;
        public string ClosedDuct = null;
        public string Size = null;
        public string SDRSystemType = null;
        public string PipeJoint = null;

        public string InstallationSpace = null;
        public string InsulationThickness = null;
        public string PriorityANDSpecial = null;
        public string GoThroughWallORBeam = null;
    }

    public class DRLine
    {
        public string DiameterDN = null;
        public string SDRSystemType = null;
        public string PipeMaterial = null;
        public string PipeJoint = null;
        public string TrapORCleanout = null;

        public string Bend45ORBend90 = null;
        public string InstallationSpace = null;
        public string InsulationThickness = null;
        public string Slope = null;
        public string Priority = null;

        public string GoThroughWallORBeam = null;
    }
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
