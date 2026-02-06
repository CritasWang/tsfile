1. TsFile 简介

2. 文件结构
   2.1 整体描述
   TsFile针对时序数据，在存储方式、编码压缩和查询读取方面做了许多优化。
   首先是按块存储，一个数据采集点的数据是以块为单位连续存储的，如果读取一个时间段的数据，它能大幅减少随机读取操作，成数量级地提升查询速度。每块内部采用列式存储，对于不同数据类型，采用不同压缩算法，而且由于一个数据采集点（以下简称测点，即Measurement）的数据变化缓慢，因此压缩率较高。索引区与数据区分离，单独保存在文件尾部，避免了在数据连续读取时，需要频繁跳过不需要的索引的问题。
   基于以上的设计原则，如果有若干个测点直接依附于一个物理实体，我们将该物理实体称为一个设备（Device）。例如，一个CPU具有温度、电压、各核心的频率等诸多测点，这些测点直接依附于这个CPU存在，因此我们把这个CPU称为这些测点的设备。这个CPU可能属于某一台服务器，而这台服务器又可能属于某个机架，但是上述测点并不直接依附于这些服务器和机架，因此服务器和机架均不被认为是这些测点的设备。
   图2.1给出了设备和测点关系的示意。从简单到复杂，复合传感器、CPU、发动机、风力发电机、地铁列车等可以采集时间序列数据的物理实体在TsFile中均被称为一个设备。一个设备生成一条时间序列时，被采集的对象称为一个测点。一个测点可以是温度、电压、转速等具有物理性质的变量，即物理量，它们通常是数值或者布尔类型；也可以是对被观测对象更复杂的描述，这种描述无法用一个简单的数字代表，例如日志甚至是图片等Blob，它们在TsFile中被抽象为（二进制）文本。
   暂时无法在飞书文档外展示此内容
   图 2.1 设备预测点结构示意图
   在TsFile中，一个设备的数据被存储为若干个ChunkGroup。在每个ChunkGroup中，一个测点的数据被存储为一个Chunk。同一个设备的不同ChunkGroup可以包含不同测点，因此ChunkGroup的存在并不对设备测点的增删产生阻碍。在实际的工业场景中，大多会根据时间区间查询某些工况信息，所以也需要针对时间区间进行抽象，因此将chunk数据按时间区间再划分为若干的Page。数据区的层级结构如图2.1和图2.2所示。
   [图片]
   图 2.2 数据区层级结构图a
   因此一条时序数据在TsFile中被分片组织在若干Chunk中，每个Chunk仅包含一条序列在一段连续时间窗口内的数据。通常情况下，各个Chunk之间时间范围互不重叠且保持有序。Chunk内部包含若干Page，这些Page之间严格保持数据的有序性且时间范围互不重叠，Page内的数据点也按照时间戳有序存储。
   TsFile文件内数据的有序性有助于在查询数据时快速对Chunk、Page进行筛选，提升数据检索效率。除此之外，TsFile内各层级还会在头信息中保存局部数据的摘要信息，如最大最小值、布隆过滤器等，在处理与数值相关的筛选条件时，能够快速跳过不符合条件的部分数据。
   [图片]
   图 2.3 数据区层级结构图b
   基于这样的数据结构，就可以建立索引树，从而充分利用边缘端有限的内存资源，最大程度的减少磁盘的IO，并加速查询。如图2.3所示，索引区包含：
3. 记录每个Chunk的文件位置和统计信息的ChunkMetadata；
4. 包含同一条时间序列下所有ChunkMetadata的TimeseriesMetadata；
5. 以TimeseriesMetadata为叶子节点，其上由各类MetadataIndexNode构成一颗B+Tree；
6. 指向同一个设备下若干条时间序列的TimeseriesMetadata的LEAF_MEASURMENT；
7. 指向同一个设备下全部时间序列的LEAF_MEASURMENT的INTERNAL_MEASURMENT；
8. 指向同一张表下若干设备的INTERNAL_MEASURMENT的LEAF_DEVICE；
9. 指向同一张表下全部设备的LEAF_DEVICE的INTERNAL_DEVICE；
10. 指向所有表的INTERNAL_DEVICE的TsFileMetadata。
    暂时无法在飞书文档外展示此内容
    图 2.4 索引区结构示意图
    图2.5展示的是 TsFile 在磁盘上的存储结构。它包括四台设备(root, db1, d1)、 (root, db1, d2)、 (db2, t1, d1)、(db2, t1, d2)，每台设备分别包含2个传感器s1、s2，共8条时间序列。每个时间序列包含1个Chunk。这些数据块在磁盘上按照行优先的顺序组织，例如首先是“TsFile”字符串及版本号，然后是“root.db1.d1”对应的ChunkGroup，然后是“root.db1.d2”对应的ChunkGroup……以此类推，最后是一个“TsFile”字符串。
    暂时无法在飞书文档外展示此内容
    图 2.5 TsFileV3在磁盘上的存储结构示意图
    暂时无法在飞书文档外展示此内容
    图 2.6 TsFileV4在磁盘上的存储结构示意图
    基于树状索引，可以加快查询速率，比如查询该 TsFile 中 root.db1.d1.s1 的全部时序数据可以按照以下步骤：
    （1）读取 TsFileMetadata，根据BloomFilter，确认该文件中存在对应序列；
    （2）在TsFileMetadata中根据表名（root, db1）找到对应的LEAF_DEVICE（图2.4中的第一个LEAF_DEVICE）；
    （3）读取LEAF_DEVICE，根据设备名（d1）找到对应的LEAF_MEASUREMENT（图2.4中的第一个LEAF_MEASUREMENT）;
    (4) 读取LEAF_MEASUREMENT，根据测点名 s1 找到对应的TimeseriesMetadata（图2.4中的第一个TimeseriesMetadata）;
    (5) 读取TimeseriesMetadata以及其中的所有ChunkMetadata；
    (6) 根据ChunkMetadata找到对应的Chunk并读取。

2.2 结构详解
2.2.1 数据区
2.2.1.1 Page
一个Page存储了一段时间序列，是数据块被压缩的最小单元（即数据按照Page，一段一段地进行压缩）。它包含一个PageHeader和实际的数据(分别编码的时间列和值列)。如表2.1所示，PageHeader包含以下成员。
表 2.1 PageHeader 成员表
成员
类型
解释
uncompressedSize
int
压缩前数据大小
compressedSize
int
压缩后数据大小
statistics
Statistics
统计信息
其中统计信息Statistics包含表2.2所示的成员。
表 2.2 Statistics 成员表
成员
类型
解释
count
long
数据点个数
startTime
long
开始时间
endTime
long
结束时间
minValue
与序列数据类型相同
最小值
maxValue
与序列数据类型相同
最大值
firstValue
与序列数据类型相同
最初值
lastValue
与序列数据类型相同
最后值
sumValue
double
值之和
2.2.1.2 Chunk
一个Chunk存储了一个测点一段时间的数据，Chunk内数据是按时间递增序存储的。它由一个字节的分隔符、一个ChunkHeader和若干个Page构成。如果该Chunk只有一页，那么分隔符是0x05；如果有多页，那么分隔符是0x01。这些分隔符用于提示文件读取器（FileReader）接下来该使用怎样的反序列化器（Deserializer）。Chunk是IO的单位，换句话说，首先会在内存里生成一整个Chunk再往外写，因此写分隔符的时候这个Chunk有几个Page是确定的。
如表2.3所示，ChunkHeader包含以下成员。
表 2.3 ChunkHeader 成员表
成员
类型
解释
chunkType
byte

标志位
第1位：是否有多个Page
第7位：是否为对齐序列的值列
第8位：是否为对其序列的时间列
measurementID
String
测点名称
dataType
TSDataType
数据类型
compressionType
CompressionType
压缩类型
encodingType
TSEncoding
编码类型
numOfPages
int
包含的Page数
dataSize
int
Chunk大小
这里对measurementID使用了变长字符串来存储。这是因为TsFile要兼顾流式写入和错误恢复的功能。如果对于这些字符串进行编码，在文件没有顺利关闭时，这些编码信息将丢失，导致之前写下去的数据无法解析。
2.2.1.3 ChunkGroup
ChunkGroup存储了一个设备实体下多个测点在一段时间的数据。由一个ChunkGroupHeader、若干个Chunk和一个字节的分隔符0x00组成。如表2.4所示，ChunkGroupHeader包含以下成员。
表 2.4 ChunkGroupHeader成员表
成员
类型
解释
deviceID
IDeviceId
设备名称
dataSize
int
该 ChunkGroup 的Chunk 大小之和
2.2.2 索引区
2.2.2.1 ChunkMetadata
它是数据块Chunk的索引信息，记录了其偏移位置和统计信息。表2.5是它的成员。
表 2.5 ChunkMeta成员表
成员
类型
解释
offsetOfChunkHeader
long
ChunkHeader 在 TsFile 的偏移
statistics
Statistics
统计信息
2.2.2.2 TimeseriesMetadata
它是时间序列索引，包含一个或多个ChunkMetadata。表2.6是它的成员。
表 2.6 TimeseriesIndex成员表
成员
类型
解释
timeSeriesMetadataType
byte
标志位。第一位为1表示该序列有多个 Chunk，否则只有1个 Chunk。第七位为1表示该序列为某多元序列的值列。第八位为1标识该序列为某多元序列的时间列。
measurementId
String
测点名称
dataType
TSDataType
数据类型
chunkMetaDataListDataSize
varInt
该序列中的 ChunkMetadata 的总大小
statistic
Statistic
统计信息
chunkMetadataListBuffer
PublicBAOS
序列化后的 ChunkMetadata
2.2.2.3 MetadataIndexNode
它是TimeseriesMetadata的索引节点。每一个表下的时间序列形成一棵索引树。这棵树由两部分组成：设备索引部分和测点索引部分。索引节点类型有四种，分别是INTERNAL_DEVICE、LEAF_DEVICE、INTERNAL_MEASUREMENT、LEAF_MEASUREMENT，分别对应设备索引部分的中间节点和叶子节点，和测点索引部分的中间节点和叶子节点。只有测点索引部分的叶子节点(LEAF_MEASUREMENT)指向 TimeseriesMetadata。
索引采用树形结构的作用是在设备数或者测点数量过大时，可以不用一次读取所有的TimeseriesMetadata，只需要根据所读取的测点定位对应的节点，从而减少 I/O，加快查询速度。

- MetaIndexNode
  索引节点MetaIndexNode的成员如表2.7所示。
  表 2.7 MetaIndexNode 成员表
  成员
  类型
  解释
  children
  List<IMetaIndexEntry>
  子节点列表
  endOffset
  long
  最后一个子节点的结束偏移量
  nodeType
  MetaIndexNodeType
  节点类型，即INTERNAL_DEVICE、LEAF_DEVICE、INTERNAL_MEASUREMENT、LEAF_MEASUREMENT
- IMetaIndexEntry
  索引项IMetaIndexEntry有两种实现，MeasurementMetadataIndexEntry和DeviceMetadataIndexEntry，分别指向INTERNAL_MEASUREMENT、LEAF_MEASUREMENT和INTERNAL_DEVICE、LEAF_DEVICE。他们的成员如表2.8和2.9所示。
  表 2.8 MeasurementMetadataIndexEntry 成员表
  成员
  类型
  解释
  name
  String
  测点名
  offset
  long
  子节点开始偏移量
  表 2.9 DeviceMetadataIndexEntry 成员表
  成员
  类型
  解释
  deviceId
  IDeviceId
  测点名
  offset
  long
  子节点开始偏移量
  2.2.2.4 TsFileMeta
  如表2.10所示，文件末尾的TsFileMeta包含每棵索引树的根节点偏移、索引区偏移量、布隆过滤器和表的模式等。
  在文件没有正常关闭时，其中的tableSchemas将发生丢失。因此在文件恢复时，恢复者需要重新注册tableSchemas。

表 2.10 TsFileMeta成员表
成员
类型
解释
bloomFilter
BloomFilter
以时间序列全名为键的布隆过滤器
tableIndexRoots
Map<String, MetadataIndexNode>
表名到表索引树根节点的映射
metaOffset
long
索引区的开始位置（含字节标记）
tableSchemas
Map<String, TableSchema>
每一张表的模式信息，即其中所有列的信息，键为表名
tsfileProperties
Map<String, String>
和TsFile有关的其他属性，例如加密算法类型、文件密钥等
表 2.11 TableSchema成员表
成员
类型
解释
tableName
String
表名
columnSchemaList
List<IMeasurementSchema>
每一列的模式信息
columnTypes
List<ColumnType>
每一列的类型，即ID或MEASUREMENT

3. 写入流程
   图3.1展示了以调用TsFileWriter的writeRecord接口为例的写入流程。其包含注册元数据、写入数据、数据刷盘、建立索引并结束文件四个步骤。
   暂时无法在飞书文档外展示此内容
   图3.1 以 writeRecord 为例的写入流程
   3.1 注册元数据
   与 TsFile 的元数据相关的类如图3.2所示。在TsFileWriter中，使用一个Schema存储元数据。每一个设备在其中都对应一个MeasurementGroup，记录了该设备下每个测点的时序元数据，用户在写入数据之前，需要先调用TsFileWriter的registerTimeseries()函数将时间序列的设备名、测点名、数据类型、编码类型、压缩类型保存到该Schema中。
   如果写入的是表数据，还需要注册每个表的列信息，即TableSchema。TableSchema包含在写入该文件时，某张表所具有的所有标识列和测点列的名字与属性。
   暂时无法在飞书文档外展示此内容
   图3.2 TsFile 元数据相关类图
   3.2 写入数据
   数据的写入会主要用到TsFileWriter、ChunkWriter和PageWriter这三个类。调用关系如图3.1“二、写入数据”所示。TsFileWriter提供的相关接口如下：
   public void registerTable(TsFileTableSchema schema);
   public boolean writeTable(Tablet tablet, List<Pair<IDeviceID, Integer>> deviceIdEndIndexPairs);
   public boolean writeRecord(TsRecord record);
   流程：
   3.2.1 TsFileWriter写入数据
   3.2.1.1 树视图
   3.2.1.1.1 以TsRecord的方式写入：
1. 调用checkIsTimeSeriesExists()函数，检查要写入的TsRecord对象的元数据是否符合之前注册的元数据；
1. 一个TsRecord对象可能包含同一时间戳下的多个测点，在checkIsTimeSeriesExists()函数的执行过程中，如果有测点是第一次写入，还会为对应测点创建各自的ChunkWriter并初始化；
1. 遍历每个测点，使用对应的ChunkWriter的write()函数将时间戳和测点值写入。
   3.2.1.1.2 以Tablet的方式写入：
   Tablet 是一个设备以列式存储的若干行数据，每一行对应相同的时间戳。它可以减少写入函数的调用次数，具有较高的写入效率；它支持写入空值，通过BitMap 标记空值。
1. 调用checkIsTimeSeriesExists()函数，检查要写入的Tablet对象的元数据是否符合之前注册的元数据；
1. 一个Tablet对象包含同一时间戳下的多个测点，在checkIsTimeSeriesExists()函数的执行过程中，还会为每个测点创建各自的ChunkWriter并初始化；
1. 遍历每个测点，使用ChunkWriter的write()函数将该测点对应的时间戳数组和值数组写入；
1. 循环检测Bitmap的每行是否为空，若不为空就使用ChunkWriter的write()函数写入。
   3.2.1.2 表视图
   以Tablet的方式写入：
1. 调用checkIsTableExists()函数，检查要写入的Tablet对象的元数据是否符合之前注册的元数据；
1. 如果deviceIdEndIndexPairs为null，将该Tablet按deviceId进行拆分，否则使用deviceIdEndIndexPairs作为按设备拆分的结果；
1. 例如，一个有10行数据的Tablet，前3行属于设备("table1", "d1")， 接下来5行属于设备("table1", "d2")，最后2行属于设备("table1", "d3")，则deviceIdEndIndexPairs的内容为[(("table1", "d1"), 3), (("table1", "d2"), 8), (("table1", "d3"), 10)]
1. 对于每个deviceId对应的拆分结果，找到或者创建其对应的ChunkGroupWriter；
1. 这一步创建的ChunkGroupWriter是否为对齐版本由参数isTableWriteAligned决定；
1. 将该deviceId所对应的拆分结果写入到ChunkGroupWriter；

1. ChunkWriter写入一个时间-值对的具体过程：
1. 识别值的数据类型并检查是否与元数据一致，不一致则抛出异常；
1. 调用PageWriter的write()函数将时间-值对写入；
1. 检查当前Page是否写满了，如果是，就执行d，否则这次写入结束；
1. 将当前PageWriter的statistic合并到ChunkWriter的statistic，然后调用PageWriter的writePageHeaderAndDataIntoBuff()函数，为当前Page生成PageHeader，将这批数据压缩成一为PageData，依次把PageHeader、PageData写入Chunk里面。
1. PageWriter写入一个时间-值对的具体过程：
1. 识别值的数据类型并检查是否与元数据一致，不一致则抛出异常；
1. 对时间戳进行编码，写入timeOutStream；对值编码，写入valueOutStream\_；然后更新statistic。

3.3 数据刷盘
数据的刷盘会主要用到TsFileWriter和TsFileIOWriter这两个类。
3.3.1 相关类：
TsFileIOWriter：
该类的主要功能是向文件进行写入序列化后的 TsFile 结构体，例如ChunkGroupHeader、Chunk等。同时该类也承担了为 TsFile 生成尾部索引的作用。该类将文件流使用接口TsFileOutput进行封装，以支持本地文件、HDFS 文件等多种文件。
这个类主要在数据刷盘（flush）阶段和结束写入（close）阶段用到。
3.3.2 流程：

1. TsFileIOWriter执行startFile()
   向TsFileIOWriter的TsFileOutput写入字符串"TsFile"和版本号；
2. TsFileIOWriter执行startChunkGroup()
3. 向TsFileIOWriter的TsFileOutput写入CHUNK_GROUP_HEADER_MARKER和设备名；
4. 创建该ChunkGroup的ChunkGroupMetadata的内存，然后用设备名来初始化；
5. ChunkGroupWriter执行flushToFileWriter()
6. 遍历该设备的ChunkGroup下的每个测点的ChunkWriter；
7. 调用ChunkWriter的sealCurrentPage()对该Chunk的最后一个Page进行封口，同时将所包含的Page数量和dataSize写入ChunkHeader。
8. 调用TsFileIOWriter的startFlushChunk()，生成当前Chunk的ChunkMetadata以及ChunkHeader。然后把ChunkHeader写入TsFileIOWriter的TsFileOutput中。
9. 调用TsFileIOWriter的writeBytesToStream()，先将Chunk的chunkData写到TsFileOutput中，然后刷到磁盘;
10. 调用TsFileIOWriter的endFlushChunk()，将3.c.生成的ChunkMetadata插入到ChunkGroupMetadata中。
    不同的ChunkGroup通过分隔符0来区分，同一ChunkGroup下的不同Chunk连续存储。
11. TsFileIOWriter执行endChunkGroup()
12. 将2.b.生成的ChunkGroupMetadata插入到 chunkGroupMetadataList中;
13. 如果该ChunkGroupMetadata为树上的 Device，在Schema中为对应的逻辑表更新列信息;
14. 如果Schema中没有该 Device 对应的表的TableSchema，为其创建一个LogicalTableSchema并放入到Schema中；
15. 使用该 Device 的 DeviceId 对应的层数更新LogicalTableSchema中的maxLevel（即取最大值）；
16. 遍历该ChunkGroupMetadata的ChunkMetadata
17. 如果LogicalTableSchema中不存在对应的MeasurementSchema，则将其添加到LogicalTableSchema中；
18. 如果已存在对应的MeasurementSchema，但是其中的数据类型与ChunkMetadata中的不一致，则将该MeasurementSchema中的数据类型设置为TEXT。
    3.4 建立索引并结束文件
    主要用到TsFileIOWriter这个类。
    根据内存中缓存的元数据，即上一小节2.b.生成的ChunkGroupMetadata和3.c.生成的ChunkMetadata，生成TsFileMetadata并追加到文件尾部，最后关闭文件。
    3.4.1 作用：
    生成TsFileMetadata的过程中的关键一步是建立元数据索引 (MetadataIndex) 树。元数据索引采用树形结构进行设计的目的是在设备数或者测点数量过大时，可以不用一次读取所有的TimeseriesMetadata，只需要根据所读取的传感器定位对应的节点，从而减少 I/O，加快查询速度。
    3.4.2 流程：
    TsFile 的索引区以自底向上的方式构建。
19. 每一个 Chunk 在生成时会对应生成一个 ChunkMetadata，这些 ChunkMetadata 被缓存在内存中或是暂存在另一个文件中；
20. 对于每一条时间序列，它所对应的所有ChunkMetadata 被汇总，记录在一个 TimeseriesMetadata 中；
21. 对于每一个设备，属于该设备的 TimeseriesMetadata 按照一定数量（该值记为 maxDegree）进行分组，每一组记录在一个类型为 LEAF_MEASUREMENT 的 MetadataIndexNode 父节点中；在此步骤中，每生成一个 MetadataIndexNode，在其中记录文件当前偏移量, 并将其包含的 TimeseriesMetadata 写到文件中；
22. 对于某设备生成的类型为LEAF_MEASUREMENT 的 MetadataIndexNode，如果其个数超过一，同样按照 maxDegree 分组构造类型为 INTERNAL_MEASUREMENT 的 MetadataIndexNode 父节点；并对这些父节点递归地构造类型为 INTERNAL_MEASUREMENT 的 MetadataIndexNode父节点，直到构造出一个根节点；在此步骤中，每生成一个 MetadataIndexNode，在其中记录文件当前偏移量，将其包含的子节点写到文件中；
23. 对于所有设备生成的构造类型为INTERNAL_MEASUREMENT 的 MetadataIndexNode根节点，按照设备所属的表（对于树模型设备，使用数据库名作为临时表名）进行分组；
24. 对于每张表下的设备，仿照第3、4步为其构造类型为 LEAF_DEVICE 和 INTERNAL_DEVICE 的 MetadataIndexNode；最终每个表有一个MetadataIndexNode根节点；
25. 将每张表的 MetadataIndexNode 根节点写入到文件，并将其偏移量记录到一个 TsFileMetadata 中；
26. 将每张表的 TableSchema 记录到 TsFileMetadata中，最后将 TsFileMetadata 写入到文件中；
27. 对于LogicTableSchema，它会根据此时的maxLevel，生成对应个数的标识列信息（第i列列名为 “\_\_Leveli”，类型均为 Text）加到数据刷盘4.b.iii步所生成的测点列MeasurementSchema之前；这些生成的标识列与测点列合并作为最终的列信息。
28. 查询流程 @谷新豪
    4.1 查询机制
    4.1.1 树视图
    TsFileExecutor接收一个QueryExpression，执行该查询并返回相应的QueryDataSet。基本工作流程如下：
    （1）TsFileExecutor接收一个QueryExpression。
    （2）基于该QueryExpression是否包含过滤条件，会走不同的执行逻辑：
    ①如果无过滤条件，执行归并查询（通过QDSWithoutTimeGenerator组件）。
    ②如果存在过滤条件，则先通过ExpressionOptimizer对该QueryExpression的Filter进行重写优化。然后判断过滤条件的类型：如果是GlobalTimeExpression，执行归并查询（通过QDSWithoutTimeGenerator组件）；如果包含值过滤，则执行连接查询（通过QDSWithTimeGenerator组件）。
    （3）生成对应的QueryDataSet，迭代地生成RowRecord，将查询结果返回。
    4.1.2 表视图
    表视图在类TableQueryExecutor提供的查询接口如下。
    // 时间列过滤条件、标识列过滤条件、测点列过滤条件最后是取 and
    // 相当于 where 时间列过滤条件 and 标识列过滤条件 and 测点列过滤条件
    RecordReader query(String tableName, // 要查询的表名
    List<String> columnNames, // 要查询的列名
    ExpressionTree timeFilter, // 时间列的过滤条件
    ExpressionTree idFilter, // 标识列的过滤条件
    ExpressionTree measurementFilter); // 测点列的过滤条件
    相关的类如图4.1所示。
    暂时无法在飞书文档外展示此内容
    图4.1 表视图查询相关类图
    该接口的查询流程如下：
29. 读取该文件的TsFileMetadata；
30. 从TsFileMetadata中读取tableName所对应索引树根节点tableRoot和表的模式信息tableSchema；
31. 如果tableRoot或tableSchema为空，构造一个空的RecordReader并返回；
32. 通过tableSchema建立columnNames的ColumnMapping；
33. 如果有，将measurementFilter中不存在于columnNames的列加入到ColumnMapping中；
34. 根据tableRoot、ColumnMapping和idFilter构造出一个DeviceTaskIterator；
35. 根据QueryExecutor中指定的结果集顺序、timeFilter、measurementFilter以及批大小blockSize：
36. 如果结果集顺序为设备顺序，构造一个DeviceOrderedBlockReader并返回；
37. 如果结果集顺序为时间顺序，构造一个TimeOrderedBlockReader并返回。

DeviceOrderedBlockReader的查询流程如下：

1. 从DeviceTaskIterator取出一个DeviceQueryTask，为其构造一个SingleDeviceRecordReader;
2. 从SingleDeviceRecordReader返回结果，直到其没有更多结果；
3. 重复1和2直到DeviceTaskIterator没有更多结果。

TimeOrderedBlockReader的查询流程如下（本版本暂不实现）：

1. 遍历DeviceTaskIterator，为每一个DeviceQueryTask构造一个SingleDeviceRecordReader，将其放入一个最小堆heap中，该堆以SingleDeviceRecordReader的当前TsBlock中的下一个时间为键;
2. 从heap返回结果，直到其没有更多结果。

SingleDeviceBlockReader的查询流程如下：

1. 根据TableSchema、task中的columnNames以及blockSize构造一个TsBlock；
2. 为task的ColumnMapping的所有measurementColumns构建一个若干AbstractFileSeriesReader，并从每个AbstractFileSeriesReader读取一批数据BatchData；
3. 如果该SingleDeviceBlockReader对应的设备包含对齐序列，则构造一个对齐的AbstractFileSeriesReader；否则为该设备下的每一个序列单独构造一个AbstractFileSeriesReader；
4. 遍历所有BatchData的当前时间戳，找到最小的当前时间戳以及对应的BatchData；
5. 将该最小时间戳以及对应BatchData的数据填充到TsBlock的对应列；
6. 如果BatchData耗尽，从其对应的AbstractFileSeriesReader读取下一个BatchData；
7. 重复3-5，直到TsBlock中的时间戳数达到blockSize，或者所有AbstractFileSeriesReader都耗尽；
8. 根据deviceId填充TsBlock中对应标识列的部分；
9. 为TsBlock中值的个数小于时间戳个数的列，使用空值进行补齐；
10. 返回当前的TsBlock。

查询组件
4.1.1 ChunkReader
最底层的读取类，直接与TsFile文件交互。
根据ChunkMeta索引定位到文件中Chunk的位置，然后对每页page：读取二进制流，解压缩、解码，封装成一个TsBlock。
为每个时间序列申请一个ChunkReader，然后该时序的多个Chunk的读取都由它来完成。
int load_by_meta(ChunkMeta \*meta)

1. 根据chunk*meta*->offset*of_chunk_header*从文件中读取一部分数据到in*stream*；
2. 反序列化得到chunk*header*；
3. 初始化value decoder和解压缩器；
4. 更新chunk*visit_offset*，让它指向in*stream*中chunk*header*的最后。
   int get_cur_page_header()
5. 从in*stream*中反序列化，得到cur*page_header*；（如果in*stream*中缓存的数据不够反序列化PageHeader的，就调用read_from_file_and_rewrap(int want_size)重新缓存一块）；
6. 更新chunk*visit_offset*，让它指向in*stream*中该page header的最后。
   bool cur*page_statisify_filter(Filter \_filter)
   根据statistics来检查当前Page的数据是否满足filter。
   int decode_cur_page_data(TsBlock *&ret_tsblock, Filter \*filter)
   （将一页page的完整数据封装成TsBlock）
7. 检查当前in*stream*中缓存的数据是否足够读出一个page，如果不够，就调用read_from_file_and_rewrap(int want_size)重新缓存一页；
8. 对该压缩的二进制流进行解压缩，
9. 从解压后的buf中提取出编码后的encoded_time_data和encoded_value_data；
10. 分别对encoded_time_data和encoded_value_data进行解码，然后封装成TsBlock返回给上层。（但有可能该Page的数据太多，在一个TsBlock中放不下，因此每次调用next()函数时需要先检查）。
    int get_next_page(TsBlock *ret_tsblock, Filter *oneshoot_filter)
11. 先针对上一页page做检查（是否都读完了）和收尾（解压缩器和解码器的重置）工作；
12. 进入while循环：
13. 检查该Chunk是否还有数据，没有了就直接退出；
14. 获取新一页的page header；
15. 检查该page是否满足过滤条件，如果不满足，重新获取下一页；
16. 将该页page的完整数据封装成TsBlock，返回给上层。

4.1.2 TsFileSeriesScanIterator
实质：ChunkReader+TimeseriesIndex
功能：该组件用于查询一个文件中单个时间序列满足过滤条件的数据点。根据给定的查询路径和被查询的文件，按照时间戳递增的顺序查询出该时间序列在文件中的所有数据点。其中过滤条件可为空。
实现：该组件首先获取给定的路径查询出所有 Chunk 的信息（即TimeseriesIndex），然后按照起始时间戳从小到大的顺序遍历每个 Chunk，并从中读取出满足条件的数据点。
成员：TimeseriesIndex（该时序的全部chunk的索引信息）、SimpleList<ChunkMeta\*>::Iterator（该时序的索引扫描迭代器）、ChunkReader（负责读Chunk数据）等。
方法：用户调用get_next()函数，每次返回一个一个Page大小的TsBlock。内部调用ChunkReader的get_next_page()函数。
int init_chunk_reader()

1. 初始化ChunkReader；
2. 将timeseries*index*的第一个Chunk Meta加载到ChunkReader；
3. chunk*meta_cursor*++。
   int get_next(TsBlock *&ret_tsblock, bool alloc, Filter *oneshoot_filter)
4. 检查ChunkReader绑定的当前Chunk是否还有数据：
5. 如果有，那简单了，直接调用ChunkReader的get_next_page()方法即可返回一个TsBlock；
6. 如果没有了，那就复杂一点。进入while循环：
7. 是否还有下一块Chunk？
8. 如果没有，直接返回E_NO_MORE_DATA
9. 如果有，获取下一个Chunk Meta，检查是否满足filter过滤条件？
10. 若满足，则将该Chunk Meta加载到ChunkReader，然后调用ChunkReader的get_next_page()方法即可返回一个TsBlock；
11. 若不满足，继续循环。
    4.1.3 TsFileIOReader
    功能：为TsFileSeriesScanIterator对象分配并加载TimeseriesIndex。
    成员：ReadFile对象（负责打开文件）、TsFileMeta对象（负责加载索引）等。
    方法：除了下面两个主要的方法，其余方法都是加载树状索引信息的。
    int load_tsfile_meta()
12. 从文件中读取tsfile_meta_size；
13. 从文件中读取TsFileMeta；
14. 将TsFileMeta反序列化。
    int alloc_ssi(string &device,string &measurement,TsFileSeriesScanIterator *&ssi,Filter *time_filter)
15. 如果还没有将文件中的TsFileMeta加载到内存，先调用load_tsfile_meta();
16. 初始化TsFileSeriesScanIterator；
17. 调用load_timeseries_index_for_ssi()函数为它分配对应设备下对应传感器的TimeseriesIndex；
18. 检查TimeseriesIndex的statistics是否满足过滤条件：
19. 如果不满足，返回E_NO_MORE_DATA；
20. 如果满足，就初始化ChunkReader，加载第一块Chunk。

4.1.4 TsFileExecutor
功能：TsFile查询执行器
成员：QueryExpression（可执行的查询表达式）、TsFileIOReader、【每条时间序列的扫描迭代器（ssi）、每条时间序列的TsBlock（用于存储数据）、每条时间序列的time对应的ColIterator、每条时间序列的value对应的ColIterator。（没有用到）】
int init(ReadFile *read*file)
调用io_reader*的init()函数。
int execute(QueryExpression *query_expr, QueryDataSet \*&ret_qds)

1. 先检查是否有查询表达式：
1. 如果有，调用optimize()函数进行优化，转化为可执行的表达式；
1. 如果没有，调用 int execute*may_with_global_timefilter(QueryExpression \_qe,QueryDataSet *&ret_qds)
1. 如果表达式类型是GLOBALTIME*EXPR，调用 int execute_may_with_global_timefilter(QueryExpression \_qe,QueryDataSet *&ret_qds)
1. 否则调用int execute*with_timegenerator(QueryExpression \_qe,QueryDataSet *&ret_qds)
   4.1.5 TsFileReader
   它是提供给用户使用的，就三个接口。
1. 初始化阶段：在open()函数中根据输入的文件名打开文件，并初始化tsfile*executor*。
1. 执行阶段：在query()函数中调用tsfile*executor*的execute()函数执行输入的查询表达式，返回查询结果集。
1. 清理阶段：在destroy*query_data_set()函数中调用tsfile_executor*的destroy*query_data_set()函数，销毁查询结果集。
   RowRecord
   class RowRecord
   {
   private:
   int64_t time*; // time value
   uint32*t col_num*; // measurement num
   std::vector<Field*> *fields*; // measurement value
   };
   它主要用于将查询结果返回给用户，需要先将不同时间序列按照时间戳对齐。
   4.1.6 QDSWithoutTimeGenerator
   功能：一次查询所返回的结果，具有相同时间戳的数据点合并为一个RowRecord。QueryDataSet 提供两个基本的功能（1）判断是否还有下一个 RowRecord；（2）返回下一个 RowRecord。
   成员：QueryExpression（可执行的查询表达式）、TsFileIOReader、每条时间序列的扫描迭代器（ssi）、每条时间序列的TsBlock（用于存储数据）、每条时间序列的time对应的ColIterator、每条时间序列的value对应的ColIterator。 heap_time*、 RowRecord \*row*record*;
   关于TsBlock：
   它的申请是由TsFileSeriesScanIterator的alloc*tsblock()函数完成的，在内部同时进行TsBlock的init();
   它的数据打包是由ChunkReader的decode_tv_buf_into_tsblock_by_datatype()函数完成的，在内部由RowAppender进行写入。
   因此每个TsBlock采用的是内存固定的策略，默认大小为g_config_value*.tsblock*max_memory* = 512000；然后每页Page打包成一个TsBlock，如果Page太大，就再打包一个TsBlock。
1. int init(TsFileIOReader *io_reader, QueryExpression *qe);
1. 初始化ssi*vec*：要查询n个时间序列，需要为每个时间序列用io*reader*的alloc_ssi()函数构建一个TsFileSeriesScanIterator，如果有GlobalTimeExpression，则将其中的Filter传入每个TsFileSeriesScanIterator。
1. 初始化其他成员变量：
   row*record* = new RowRecord(path*count);
   tsblocks*.resize(path*count);
   time_iters*.resize(path*count);
   value_iters*.resize(path_count);
1. for循环为每个时间序列调用get*next_tsblock()，来初始化heap_time*、tsblocks*、time_iters*和value*iters*。
1. int get_next_tsblock(uint32_t index, bool alloc_mem)
1. 使用当前index对应的ssi调用get_next()函数来获取当前时间序列的一块TsBlock；
1. 用该TsBlock来初始化time*iters*[index]和value*iters*[index]这两个列式读取迭代器；
1. 使用time*iters*[index]读取当前最早的时间存入heap*time*。
1. RowRecord \*get_next()
   由于每个TsFileSeriesScanIterator会按照时间戳从小到大的顺序迭代地返回数据点，所以可以采用“多路归并”对所有TsFileSeriesScanIterator的结果进行按时间戳对齐。
   数据归并的步骤为：
1. 创建一个最小堆。（key是“时间戳”，value是“时序的编号”，该堆按照每个时间戳的大小进行排序）
1. 初始化堆，依次访问每一个TsFileSeriesScanIterator获取一块TsBlock并将最小时间戳放入堆中。此时每个时间序列最多有 1 个时间戳被放入到堆中，即该序列最小的时间戳。
1. 如果堆的 size > 0，进入步骤d；如果堆的 size 等于 0，则跳到步骤e。
1. 获取堆顶的时间戳，记为 t，并用它初始化新的RowRecord。然后在堆中找出key=t的全部pairs，依次将该数据点添加到RowRecord中，不存在的列标记为NULL。同时判断该时间序列是否有新的数据点，若存在，则将下一个时间戳 t' 添加在堆中。最后在堆中删除原时间戳t，并返回步骤c。
1. 结束归并。
   4.1.7 QDSWithTimeGenerator
   Node *construct_node_tree(Expression *expr)
   先序建树，
   int init(TsFileIOReader *io_reader, QueryExpression *qe)
   为每个ValueAt对象申请并分配ssi；
   初始化RowRecord;
   构建树。
   核心逻辑
   根据查询过滤条件，分为两种情况：
1. 情况1：没有过滤条件，或者仅有global time filter
1. 情况2：其他
   情况1：没有过滤条件，或者仅有global time filter
   比较简单，查询涉及到的多个时序进行TimeJoin即可。
   TimeJoin实现方式：每个时序提供一路输入迭代器 TsFileSeriesScanIterator，使用std::multimap<int64*t, uint32_t> heap_time*;其中key为时间戳，value为多路输入的编号。然后通过heap*time*这个结构体进行TimeJoin，具体代码见 QDSWithoutTimeGenerator::get_next()。
   情况2：情况1之外的场景
   注：目前我们只支持基于时间戳的过滤（实际上代码是支持value过滤的，但是这块没有测试计划）。
   举个例子：(s1时间戳 > 2 && s2时间戳 < 5) || (global_time > 5 && global_time < 7)
   [图片]
   例子中的查询，OR操作左侧有time=3,4两行，OR右侧有time=6一行，总计输出3行数据。
   计算过程会被拆成两个组件：一个是生成下一条记录的时间戳，称之为TimeGenerator；另一个组件取每个时序在当前时间戳的值，使用ValueAt对象进行查找。
   TimeGenerator在代码实现上是一个语法树，即图中的样子，叶子节点是每一路时序数据作为输入，根节点获取next_timestamp（代码中为 Node::get_cur_timestamp()）时，根据是OR还是AND递归对子节点进行get_cur_timestamp。例如图中，OR的左子树和右子树分别提供了time=3和time=6，则先吐出time=3，然后递归对左子树进行get_cur_timestamp。
   吐出time=3之后，对要输出的每路时序，通过ValueAt查找time=3的value值。ValueAt的逻辑是对该时序创建一个迭代器，然后检查当前迭代到的time是否为3，小于3则继续迭代找time=3，如果当前等于3则吐出该结果，如果大于3则表示该时序没有time=3的值，返回NULL。
   依次迭代完所有时间戳为止。
1. TsFile合并中TableSchema的处理
   合并中的主要流程都直接调用 TsFile 层各种接口，因此只需要完成 IDeviceID 的替换以及表模型的 TableSchema 收集
   5.1 合并前收集 schema
   5.1.1 v3 文件与 v3 文件
   文件中没有 TableSchema 信息，可以在 TsFileIOWriter 调用 endChunkGroup 时自动更新 TableSchema，endFile 时在 getColumnSchemas 时自动生成 ID 列的 schema
   5.1.2 v3 文件与 v4 树模型
   v4 文件中有 TableSchema 信息，但是不使用，依靠 ChunkGroupMetadata 自动生成。区分 v4 树模型和 v4 的表模型采用 tableName 进行判断，如果以 'root.' 开头则认为是树模型。
   5.1.3 v4 树模型与 v4 树模型
   文件中有 TableSchema 信息，但是 TableSchema 中不包含 device，没法确定其中的 IMeasurementSchema 是属于哪个设备的，直接使用这个信息可能会在不同 device 存在同名序列但是类型不同的情况下出错，因此这里也不使用，依靠 endChunkGroup 时的 ChunkGroupMetadata 自动生成
   5.1.4 v4 表模型与 v4 表模型
   文件中有 TableSchema 信息，合并一开始就设置到 TsFileIOWriter 上，且后续流程中可以不需要再通过读 ChunkHeader 的方式来收集 IMeasurementSchema
   对于同一个 Table，需要将来自不同 TableSchema 中的所有 ID 类型的列合并并去重，然后设置到 TsFileIOWriter 上，MEASUREMENT 类型的列则不收集，依靠后续 endChunkGroup 时的 ChunkGroupMetadata 自动生成
   5.2 合并两个来自不同文件的 TableSchema 中的 id 列的方式
   需要先对 id 列检查前缀，如果前缀不同则报错，否则增加一列。
   如文件 1 中 table1 的 id 列名为 id1，id2，文件 2 中 table1 的 id 列名为 id1，id3，报错。
   如文件 1 中 table1 的 id 列名为 id1，id2，文件 2 中 table1 的 id 列名为 id1，id2，id3，将 id3 合入，结果为 id1，id2，id3。
   5.3 不同类型合并任务收集 Schema 方式
   5.3.1 空间内合并
   收集所有源文件的 schema 并合并 id 列
   5.3.2 跨空间合并
   合并前不区分目标文件会有哪些 table，直接将所有参与合并任务的顺序文件和乱序文件的 schema 整合，设置给每一个目标文件，在合并完成 endFile 前进行一次校验，如果一个 table 中没有 MEASUREMENT 类型的列，说明这个目标文件中并没有合入这部分数据，可以被删掉
   5.4 有删除的情况
   在调用 TsFileIOWriter.endChunkGroup 之前，对 writer 中的 TableSchemaMap 进行一次扫描，如果某一个 TableSchema 不包含 MEASUREMENT 类型的列，说明没有调用过任何相关这个 table 的数据的 endChunkGroup 或调用时传入的 ChunkGroupMetadata 为空，也就是说目标文件中没有这个 table 的数据，可以从 TableSchemaMap 中去掉这一项
1. 兼容性
   6.1 从 PlainDeviceId 到 StringArrayDeviceId 的转换
   在 V4 之前的 TsFile 中，使用的IDeviceId实例为PlainDeviceId，使用一个字符串形如"root.a.b.c.d"来存储 DeviceId。从 V4 以后IDeviceId的实例为StringArrayDeviceId，使用一个数组形如{"root.a.b", "c", "d"}来存储 DeviceId，数组第一个元素作为表名，其后若干元素为标识列。
   一个PlainDeviceId或者一个字符串，使用如下的规则转换为StringArrayDeviceId：
1. 以"."为分割符分割该字符串；
1. 如果分割后的段数 k 等于 1 （应该只在测试中出现），则用这一段作为表名，无标识列；例如，"root"的转换结果为{"root"}；
1. 如果分割后的段数 k 小于DEFAULT_SEGMENT_NUM_FOR_TABLE_NAME + 1(一次性配置参数，默认为3)，则以前 k - 1 段按"."连接作为表名，最后一段作为标识列；例如，"root.a"的转换结果为{"root","a"},"root.a.b"的转换结果为{"root.a","b"};
1. 否则用前 DEFAULT_SEGMENT_NUM_FOR_TABLE_NAME 段按"."连接作为表名，剩余段为标识列；例如，"root.a.b.c"的转换结果为{"root.a.b", "c"}，"root.a.b.c.d"的转换结果为{"root.a.b", "c", "d"}。
   6.2 索引根节点
   V3 版本的 TsFile 只有一个索引根节点，该根节点在被V4的读取器读取时，以“”（空字符串）为键存到TsFileMetadata的tableMetadataIndexNodeMap中，获取该根节点以后可以以 V3 的查询流程（即树视图的查询流程）继续。对于树视图的查询，如果基于5.1给出的转换方式无法找到表名对应的根节点，会尝试以“”作为表名再次查询，如果均不能找到根节点，则返回空的查询结果。
   6.3 表的模式信息
   V3版本的 TsFile 没有表的模式信息，在升级前无法通过表视图的接口进行查询该 TsFile 中具有哪些由树视图转换为的表。
   6.4 表视图的查询
   但是，仍有可能通过表视图的接口查询 V3 TsFile 中的数据，只要将表名设置为“”，并提前知道相应时间序列的测点名等。
   例如，对于一个包含时间序列“root.db1.beijing.turbines.d0015.speed”的 V3 文件，有可能可以通过将列名设置为“speed”，表名为“”，标识列条件为“**level1='db1' & **level2='beijing' & **level3='turbines' & **level4=‘d0015’”的方式进行查询。

1. 实验
   7.1 实验设置
   默认参数
   表数：100
   每表设备数：100
   每设备列数：100
   每Tablet行数：100
   Tablet数：100
   对于第i张表的第j个设备的第k个测点列：
   其DeviceId为“table*{i}.0.0.{j}”(对于树)或{"table*{i}", "0", "0", "{j}"}（对于表），即标识列的个数为3；
   其MeasurementId为“s{k}”。
   所有测点列均为Long类型，使用Gorilla编码和LZ4压缩。
   对于树视图，首先进行序列注册，再使用对齐的Tablet进行写入；对于表视图，首先进行表注册，再使用Tablet进行写入。每个Tablet包含一个表的一个设备。写入结束后，选取位于中间的一个设备（如果有100个设备，则选择第50个设备），查询其下所有测点。
   实验重复10次，取后5次结果平均以消除JIT和系统缓存等因素造成的影响。
   develop 分支 （即TsFile v3）commitId：1c40e1070080b6987410a2c29a48ccaf4c09018d
   tsFile_v4 分支 commitId：a3cb8ed5f97e96f96e85ff67b1b85dfea2620048
   实验环境：
   CPU：i9-12900
   内存：32GB
   硬盘：Samsung SSD 980 1TB
   7.2 实验结果
   7.2.1 修改Tablet数
   表6.1 修改Tablet数的实验结果
   Tablet数
   对比方法
   注册耗时（ns）
   写入耗时（ns）

关闭耗时（ns）
查询耗时（ns）
文件大小（Byte）
100
v4_table
94,880
393,315,961,100
1,187,128,840
2,471,375,980
725,729,019
100
v4_tree
18,835,640
384,304,615,720
1,179,493,900
19,660,415,240
725,605,889
100
develop
15,801,860
387,061,265,600
1,046,669,920
19,717,264,140
725,222,149
10
v4_table
86,600
34,993,024,320
1,395,128,300
373,068,880
347,929,380
10
v4_tree
13,969,460
33,652,668,420
1,401,273,680
2,011,732,040
347,806,220
10
develop
16,304,900
33,728,884,180
1,219,120,360
2,004,989,920
347,422,430
1
v4_table
96,340
3,762,735,860
1,663,027,000
86,7013,60
185,241,420
1
v4_tree
14,967,980
3,685,743,160
1,734,431,800
325,839,900
185,118,320
1
develop
13,078,639
3,605,640,594
1,471,690,860
411,021,959
184,734,630
在注册耗时上，表视图只需要进行100次表注册，每次表注册包含100条序列，而树视图需要进行一百万次序列注册，表视图的注册量要少于树视图，因此 v4_table 的注册耗时要显著小于 v4_tree 和 develop。v4_tree和develop互有高低，鉴于注册过程总时长仅为数十毫秒，两者的差距可以认为在自然波动范围。
在写入耗时上，v4_tree 在 Tablet 数为100和10时略低于 develop （1%），而在tablet数为1时则略高于 develop（2%），可以认为差距是波动导致，二者性能近似相等；v4_table 比两者略高（3%），因为 v4_table 的 Tablet 中包含了标识列，一方面它们影响了内存效率，另一方面在写入前需要把 tablet 按设备进行拆分，引入了额外的计算。
在关闭耗时上，v4 的两个方法相较于 develop 有10%-20%的提升，因为 v4 的 TsFile 需要额外记录 TableSchema，并且在生成索引树时按照表生成不同的根节点，计算量相对较大。在 Tablet 数为 100 时，关闭耗时小于写入耗时的1%，在此场景下使用 v4 对写入的整体的影响不大。
在查询耗时上，由于 v4_table 在查询时为对齐序列整体生成一个 Reader，而非其他二者的对每一个分量生成一个 Reader，可以减少对时间戳的冗余读取，减少了约70%的查询耗时。v4_tree 和 develop 在 Tablet 数为100或10时没有明显差距，但是在 Tablet 数为1时 v4_tree 的耗时减少了约 20%。因为 v4 下每个表（包含树视图写入时生成的逻辑表）的索引根节点直接存储在 TsFileMetadata 中，而非只在其中存储一个总的根节点，此举减少了查询索引树的耗时。
在文件大小上，相较于develop，TableSchema 的记录略微增大了 v4 的两个方法下的文件大小，但幅度小于0.1%。
7.2.2 修改表数
表6.2 修改表数的实验结果
表数
对比方法
注册耗时（ns）
写入耗时（ns）

关闭耗时（ns）
查询耗时（ns）
文件大小（Byte）
100
v4_table
94,880
393,315,961,100
1,187,128,840
2,471,375,980
725,729,019
100
v4_tree
18,835,640
384,304,615,720
1,179,493,900
19,660,415,240
725,605,889
100
develop
15,801,860
387,061,265,600
1,046,669,920
19,717,264,140
725,222,149
10
v4_table
46,620
39,692,566,202
260,849,059
2,373,892,460
72557361
10
v4_tree
1,376,200
38,652,429,346
285,917,440
21,771,260,919
72,545,051
10
develop
1,619,560
39,187,044,984
266,452,279
22,034,953,300
72,506,696
1
v4_table
9,920
3,893,357,628
33,591,939
2,394,113,060
7,255,772
1
v4_tree
237,580
4,005,940,065
39,227,260
22,066,198,740
7,254,541
1
develop
191,980
3,926,525,921
37,381,840
22,193,664,239
7,250,694
在注册耗时上，结论与6.2.1节一致。
在写入耗时上，随着表数的下降，几种方法的差距逐渐消失。
在关闭耗时上，v4_tree 和 develop 之间差距与6.2.1节基本一致。v4_table 的关闭耗时随着表数上升而上升地更快，印证了6.2.1节所述的多表在关闭时的额外操作带来的影响。
在查询耗时上，结论与6.2.1节一致。
在文件大小上，结论与6.2.1节一致。
7.2.3 修改每设备序列数
表6.3 修改每设备序列数的实验结果
每设备序列数

对比方法
注册耗时（ns）
写入耗时（ns）

关闭耗时（ns）

查询耗时（ns）
文件大小（Byte）
100
v4_table
94,880
393,315,961,100
1,187,128,840
2,471,375,980
725,729,019
100
v4_tree
18,835,640
384,304,615,720
1,179,493,900
19,660,415,240
725,605,889
100
develop
15,801,860
387,061,265,600
1,046,669,920
19,717,264,140
725,222,149
10
v4_table
47,600
42,268,938,667
292,488,620
283,749,760
77,627,956
10
v4_tree
3,103,500
37,037,827,968
281,521,400
1,492,190,799
77,504,856
10
develop
1,613,780
36,795,995,624
264,789,860
1,484,200,980
77,283,166
1
v4_table
23,420
8,784,041,400
89,011,940
64,376,660
13,012,508
1
v4_tree
2,575,080
4,606,305,380
82,941,560
91,457,020
12,889,408
1
develop
1,301,960
4,248,784,400
74,902,860
132,511,180
12,683,018
在注册耗时上，结论与6.2.1节基本一致。由于IDeviceID的实例变更为结构上更复杂的StringArrayDeviceID，在每设备序列数较少（即 deviceId 在内存中占比较高）时，v4_tree 的表现倾向于比 develop 更差。每设备序列数为1和10时几乎多用了一倍的时间。
在写入耗时上，同样是由于 deviceId 带来的影响增大，在每设备序列数较少时 v4_table 的写入耗时会显著较长。在每设备序列数为10时，v4_table 相较于 develop 写入耗时提升了约 10%；而在每设备序列数为1时，写入耗时约提升了一倍。
在关闭耗时上，v4_tree 和 develop 之间差距与6.2.1节基本一致。
在查询耗时上，每设备序列较少时，v4_table的优势降低，但在序列数为1时能减少约一半的的耗时。
在文件大小上，每设备序列数为1时，v4 带来的文件放大更加明显，但仍少于（3%）。
7.2.4 降低 StringArrayDevice 带来的影响
在 6.2.3 节中，当每设备序列数较少时（例如为1），v4_table 相较于 develop 的写入耗时有近一倍的提升。这一点被解释为使用StringArrayDeviceId带来的影响，本小节对此进行确认。
[图片]
图6.1 使用 develop 在每设备序列数为1时的写入耗时
[图片]
图6.2 使用 v4_table 在每设备序列数为1时的写入耗时
对比图6.1和图6.2，可以发现v4_table的主要额外开销在于从Tablet获取每一行的IDeviceID时，将Tablet中标识列的Binary对象转换为String时的开销，以及对IDeviceID进行比较的开销。
针对Binary对象转换为String时的开销，Tablet中标识列的数据类型为Text时，不再使用Binary对象存储，而是使用String对象存储；针对比较IDeviceID的开销，扩展TsFileWriter的Tablet写入接口，增加参数List<Pair<IDeviceID, Integer>> deviceIdEndIndexPairs，允许接口使用者提前指定Tablet的拆分方式，可以避免在Tablet只包含一个设备时进行不必要的IDeviceID比较。
表6.4 优化 IDeviceID 处理后的实验结果
每设备序列数

对比方法
注册耗时（ns）
写入耗时（ns）

关闭耗时（ns）

查询耗时（ns）
文件大小（Byte）
1
v4_table
16,060
4,273,450,120
83,554,140
63,517,880
13,012,508
1
v4_tree
2,580,460
4,578,941,520
82,695,300
96,781,580
12,889,408
1
develop
1,701,520
4,267,127,240
82,340,900
131,574,960
12,683,018
进行优化后的结果如表6.4所示，可以发现 v4_table 的写入性能已经和 develop 基本持平。8. 空间分析
为了简化分析，本章采用下列假设或者近似处理：

1. 每个设备标识符（DeviceId）具有相同长度，并且标识符中的每一段长度相等；
2. 每个设备下具有相同的测点数，并且测点名长度相同；
3. 每条时间序列的数据量相同，只存在于一个ChunkGroup；
4. 忽略数据类型、编码方式、压缩方式的对 Page 大小和统计信息的影响，抽象为一个统一的大小；
5. 忽略索引树中的 INTERNAL_MEASUREMENT 中间节点（对于默认配置节点扇出indexFanout=1000，一个 LEAF_MEASUREMENT 节点可以存储一个设备的1_000_000条序列）；
6. 忽略索引树中每一层的最后一个节点填充度和其他节点的差异。
   表7.1 TsFile 空间分析中使用到的符号表
   符号
   释义
   符号
   释义
   devLen

设备标识符的总长度。
对于路径形式的 DeviceId，其 devLen 等于路径总长度；例如"root.a.b.c.d1"的 devLen=13。
对于数组形式的 DeviceId，其 devLen 等于数组中各元素字符数之和；例如（"root.a.b", "c", "d1"）的 devLen=8+1+2=11。
devSegCnt

DeviceId 的段数。
对于路径形式的 DeviceId，其 devSegCnt 等于分隔符数量加1；例如"root.a.b.c.d1"的 devSegCnt=5。
对于数组形式的 DeviceId，其 devLen 等于数组中元素个数；例如（"root.a.b", "c", "d1"）的 devSegCnt=3。
devMeasureCnt
每个设备的测点数
seriesChunkCnt
每条时间序列的 Chunk 数。
varIntSize(x)

一个四字节整数 x 转换为变长整数后的字节数。
0 <= x < 2^7 : 1
2^7 <= x < 2^14 : 2
2^14 <= x < 2^21 : 3
2^21 <= x < 2^28 : 4
2^28 <= x : 5
measureLen

测点名的长度。
对于“s1”，其长度为2。
chunkDataSize

一个 Chunk 中的数据大小，即其中所有 Page 及其 Header 大小之和。
size(x)
结构体 x 的大小。
pageDataSizeRaw
一个 Page 中的数据的压缩前大小，不包含 Header。
pageDataSizeComp
一个 Page 中的数据的压缩后大小，不包含 Header。
statSize
统计信息结构的大小。
pageCnt
一个 Chunk 中的 page 个数
tableCnt
表的数量。
deviceCnt
设备的总数。
indexFanout
索引树中一个节点的最大子节点数。
errorPercent
BloomFilter 的假阳性率。
hashFuncCnt
BloomFilter 的 Hash 函数个数。
internalNodeCnt(x)
在节点出度为 indexFanout 时，具有x个叶节点的树所具有的中间节点数。
表7.2 TsFile空间分析表
结构
子结构
字节数（V4）
字节数（V3）
备注
MagicString

6
6
“TsFile”字符串
VersionNum

1
1
Byte
ChunkGroupHeader
Marker
1
1
Byte

deviceID
varIntSize(devSegNum) +
devSegNum\*(varIntSize(devLen/devSegNum)) + devLen
varIntSize(devlen) +
devLen
V3中为String
V4中为String数组
Chunk
ChunkHeader
1 +
varIntSize(measurementLen) + measurementLen +
varIntSize(chunkDataSize) +
1 +
1 +
1
1 +
varIntSize(measurementLen) + measurementLen +
varIntSize(chunkDataSize) +
1 +
1 +
1
分隔符
测点名
数据大小
数据类型
压缩方式
编码方式

PageHeader
varIntSize(pageDataSizeRaw) +
varIntSize(pageDataSizeComp) +
pageCnt > 1 ? statSize : 0
varIntSize(pageDataSizeRaw) +
varIntSize(pageDataSizeComp) +
pageCnt > 1 ? statSize : 0

PageData
pageDataSizeComp
pageDataSizeComp

索引区分隔符

1
1
值为2
TimeseriesMetadata

1 +
varIntSize(measureLen) + measureLen +
1 +
varIntSize(seriesChunkCnt) +
statSize

1 +
varIntSize(measureLen) + measureLen +
1 +
varIntSize(seriesChunkCnt) +
statSize

Flag，该时间序列是否对齐以及是否包含多个 Chunk
测点名
数据类型
Chunk 个数
统计信息
ChunkMetadata

8 +
seriesChunkCnt > 1 ? statSize : 0
8 +
seriesChunkCnt > 1 ? statSize : 0
Chunk 的文件偏移量
MetadataIndexNode

LEAF*MEASUREMENT
INTERNAL_MEASUREMENT
varIntSize(min([devMeasureCnt / indexFanout], indexFanout)) +
min([devMeasureCnt / indexFanout], indexFanout) * size(MeasurementMetadataIndexEntry) +
8 +
1
varIntSize(min([devMeasureCnt / indexFanout], indexFanout)) +
min([devMeasureCnt / indexFanout], indexFanout) \_ size(MeasurementMetadataIndexEntry) +
8 +
1
子节点个数，向上取整
子节点指针大小
最后一个子节点的结束偏移量
节点类型

MeasurementMetadataIndexEntry

varIntSize(measureLen) + measureLen +
8
varIntSize(measureLen) + measureLen +
8
测点名
TimeseriesMetadata 的文件偏移量

LEAF*DEVICE
INTERNAL_DEVICE
varIntSize(min((deviceCnt / tableCnt), indexFanout)) +
min((deviceCnt / tableCnt), indexFanout) * size(DeviceMetadataIndexEntryV4) +
8 +
1
varIntSize(min(deviceCnt, indexFanout)) +
min(deviceCnt, indexFanout) \_ size(DeviceMetadataIndexEntryV3) +
8 +
1
子节点个数，向上取整
子节点指针大小
最后一个子节点的结束偏移量
节点类型

DeviceMetadataIndexEntry
size(IDeviceIDV4) +
8

size(IDeviceIDV3) +
8

DeviceId
LEAF_DEVICE 的文件偏移量
TsFileMetadata

varIntSize(tableCnt) +
tableCnt*size(LEAF_DEVICE) +
varIntSize(tableCnt) +
tableCnt*size(TableSchema) +
8 +
varIntSize(size(BloomFilter)) + size(BloomFilter) +
size(TsFileProperties)
size(LEAF_DEVICE) +
8 +
varIntSize(size(BloomFilter)) + size(BloomFilter)

8为索引区起始偏移量
TsFileProperties大小暂时难以确定

TableSchema

varIntSize(devMeasureCnt) +
devMeasureCnt \* size(MeasurementSchema) +
devMeasureCnt

不存在

列数
列模式

列类型

MeasurementSchema
4 + measureLen +
1 +
1 +
1
4 + measureLen +
1 +
1 +
1
测点名
数据类型
编码方式
压缩方式

BloomFilter
[((-(deviceCnt * deviceMeasureCnt) * Math.log(errorPercent) / ln2 / ln2) + 1) / 8] +
varIntSize(deviceCnt _ deviceMeasureCnt) +
varIntSize(hashFuncCnt)
[((-(deviceCnt _ deviceMeasureCnt) _ Math.log(errorPercent) / ln2 / ln2) + 1) / 8] +
varIntSize(deviceCnt _ deviceMeasureCnt) +
varIntSize(hashFuncCnt)
位图大小，向上取整

序列总数

TsFileMetadataSize

4
4

小计
One Chunk
size(ChunkHeader) +
pageCnt _ (size(PageHeader) + size(PageData))
size(ChunkHeader) +
pageCnt _ (size(PageHeader) + size(PageData))

One ChunkGroup
size(ChunkGroupHeaderV4) +
devMeasureCnt _ seriesChunkCnt _ size(Chunk)
size(ChunkGroupHeaderV3) +
devMeasureCnt _ seriesChunkCnt _ size(Chunk)

Data Section
deviceCnt _ size(ChunkGroupV4)
deviceCnt _ size(ChunkGroupV3)

All ChunkMetadata
deviceCnt _ devMeasureCnt _ seriesChunkCnt _ size(ChunkMetadata)
deviceCnt _ devMeasureCnt _ seriesChunkCnt _ size(ChunkMetadata)

All TimeseriesMetadata
deviceCnt _ devMeasureCnt _ size(TimeseriesMetadata)
deviceCnt _ devMeasureCnt _ size(TimeseriesMetadata)

All LEAF*MEASUREMENT
deviceCnt * [devMeasureCnt / indexFanout / indexFanout] _ size(LEAF_MEASUREMENT)
deviceCnt _ [devMeasureCnt / indexFanout / indexFanout] \_ size(LEAF_MEASUREMENT)
[]为向上取整

All LEAF_DEVICE

[(deviceCnt / tableCnt) / indexFanout] _ size(LEAF_DEVICEV4) _ tableCnt
[deviceCnt / indexFanout] \* size(LEAF_DEVICEV3)
[]为向上取整

All INTERNAL_DEVICE

internalNodeCnt([(deviceCnt / tableCnt) / indexFanout]) _ size(INTERNAL_DEVICEV4) _ tableCnt
internalNodeCnt([deviceCnt / indexFanout])\* size(INTERNAL_DEVICEV3)
[]为向上取整

Index Section

size(All ChunkMetadata) +
size(All TimeseriesMetadata) +
size(All LEAF_MEASUREMENT) +
size(All LEAF_DEVICEV4) +
size(All INTERNAL_DEVICEV4) +
size(TsFileMetadataV4)

- internalNodeCnt([(deviceCnt / tableCnt) / indexFanout]) > 0 ? tableCnt _ size(INTERNAL_DEVICEV4) ：tableCnt _ size(LEAF_DEVICEV4)
  size(All ChunkMetadata) +
  size(All TimeseriesMetadata) +
  size(All LEAF_MEASUREMENT) +
  size(All LEAF_DEVICEV3) +
  size(All INTERNAL_DEVICEV3) +
  size(TsFileMetadataV3)
- internalNodeCnt([deviceCnt / indexFanout]) > 0 ? tableCnt \* size(INTERNAL_DEVICEV3) ：size(LEAF_DEVICEV3)

Total LEAF_DEVICE 和 Total INTERNAL_DEVICE 有一部分（根节点）包含在 TsFileMetadata 中，需要减去重复部分。

TsFile
size(MagicString) +
size(VersionNum) +
size(Data SectionV4) +
size(Index SectionV4) +
4 +
size(MagicString)
size(MagicString) +
size(VersionNum) +
size(Data SectionV3) +
size(Index SectionV3) +
4 +
size(MagicString)

4为FileMetadata长度
差异（V4-V3）
ChunkGroupHeader.IDeviceId
varIntSize(devSegNum) +
devSegNum\*(varIntSize(devLen/devSegNum)) -
varIntSize(devlen)

LEAF_DEVICE
INTERNAL_DEVICE

varIntSize(min((deviceCnt / tableCnt), indexFanout)) +
min((deviceCnt / tableCnt), indexFanout) _ size(DeviceMetadataIndexEntryV4) -
varIntSize(min(deviceCnt, indexFanout)) -
min(deviceCnt, indexFanout) _ size(DeviceMetadataIndexEntryV3)

DeviceMetadataIndexEntry

varIntSize(devSegNum) +
devSegNum\*(varIntSize(devLen/devSegNum)) -
varIntSize(devlen)

TsFileMetadata
varIntSize(tableCnt) +
(tableCnt-1)*size(LEAF_DEVICE) +
varIntSize(tableCnt) +
tableCnt*size(TableSchema)

Data Section
deviceCnt _ (varIntSize(devSegNum) +
devSegNum_(varIntSize(devLen/devSegNum)) -
varIntSize(devlen))

All LEAF_DEVICE

([(deviceCnt / tableCnt) / indexFanout] - [deviceCnt / indexFanout]) \* (varIntSize(min((deviceCnt / tableCnt), indexFanout)) + 8 + 1)

总设备数相等，意味着所有 LEAF_DEVICE 中的设备指针总数相等，因此大小差距主要来源于节点的其他部分。

All INTERNAL*DEVICE
internalNodeCnt([(deviceCnt / tableCnt) / indexFanout]) * size(INTERNAL*DEVICEV4) * tableCnt - internalNodeCnt([deviceCnt / indexFanout])\* size(INTERNAL_DEVICEV3)

Index Section
varIntSize(tableCnt) +
(tableCnt-1)*size(LEAF_DEVICE) +
varIntSize(tableCnt) +
tableCnt*size(TableSchema) +
([(deviceCnt / tableCnt) / indexFanout] - [deviceCnt / indexFanout]) _ (varIntSize(min((deviceCnt / tableCnt), indexFanout)) + 8 + 1) +
internalNodeCnt([(deviceCnt / tableCnt) / indexFanout]) _ size(INTERNAL*DEVICEV4) * tableCnt - internalNodeCnt([deviceCnt / indexFanout])\_ size(INTERNAL_DEVICEV3)

TsFile
deviceCnt _ (varIntSize(devSegNum) +
devSegNum_(varIntSize(devLen/devSegNum)) -
varIntSize(devlen)) +
varIntSize(tableCnt) +
(tableCnt-1)*size(LEAF_DEVICE) +
varIntSize(tableCnt) +
tableCnt*size(TableSchema) +
([(deviceCnt / tableCnt) / indexFanout] - [deviceCnt / indexFanout]) _ (varIntSize(min((deviceCnt / tableCnt), indexFanout)) + 8 + 1) +
internalNodeCnt([(deviceCnt / tableCnt) / indexFanout]) _ size(INTERNAL*DEVICEV4) * tableCnt - internalNodeCnt([deviceCnt / indexFanout])\_ size(INTERNAL_DEVICEV3)

下面是一个与上述分析对应，可以提供参数并计算的表格。
暂时无法在飞书文档外展示此内容9. 结构示例
以下为一个 V4 版本 TsFile 的结构示例，它包含一张表“testTable0”，该表下有5个设备，从("testTable0", "0", "0", "0", "0", "0")、("testTable0", "1", "1", "1", "1", "1")到("testTable0", "4", "4", "4", "4", "4")；每个设备下有五条INT64序列，从“s0”、“s1”到“s4”。它还包含两个树设备“root.a.b.c.d1”和“root.a.b.c.d2”，分别以非对齐和对齐方式存储，每个设备下有五条INT64序列，从“s0”、“s1”到“s4”。左边一列（POSITION）代表结构体在文件中的起始偏移量，右边一列（CONTENT）表示该结构体的类型和内容。
POSITION| CONTENT

---

0| [magic head] TsFile
6| [version number] 4
||||||||||||||||||||| [ChunkGroup] of root.a.b.c.d1, num of Chunks:5
7| [ChunkGroup Header]
7| [marker] 0
8| [deviceID] root.a.b.c.d1 size=27
23| [Chunk] of root.a.b.c.d1.s0, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
23| [Chunk Header] marker=5, measurementID=s0, dataSize=429, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
32| [Page Header] HeaderSize:4, UncompressedSize:425, CompressedSize:425
36| [Page Data] Size:425
461| [Chunk] of root.a.b.c.d1.s1, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
461| [Chunk Header] marker=5, measurementID=s1, dataSize=429, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
470| [Page Header] HeaderSize:4, UncompressedSize:425, CompressedSize:425
474| [Page Data] Size:425
899| [Chunk] of root.a.b.c.d1.s2, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
899| [Chunk Header] marker=5, measurementID=s2, dataSize=429, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
908| [Page Header] HeaderSize:4, UncompressedSize:425, CompressedSize:425
912| [Page Data] Size:425
1337| [Chunk] of root.a.b.c.d1.s3, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
1337| [Chunk Header] marker=5, measurementID=s3, dataSize=429, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
1346| [Page Header] HeaderSize:4, UncompressedSize:425, CompressedSize:425
1350| [Page Data] Size:425
1775| [Chunk] of root.a.b.c.d1.s4, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
1775| [Chunk Header] marker=5, measurementID=s4, dataSize=429, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
1784| [Page Header] HeaderSize:4, UncompressedSize:425, CompressedSize:425
1788| [Page Data] Size:425
||||||||||||||||||||| [ChunkGroup] of root.a.b.c.d1 ends
||||||||||||||||||||| [ChunkGroup] of root.a.b.c.d2, num of Chunks:6
2213| [ChunkGroup Header]
2213| [marker] 0
2214| [deviceID] root.a.b.c.d2 size=27
2229| [Chunk] of root.a.b.c.d2., startTime: 0 endTime: 49 count: 50
2229| [Chunk Header] marker=-123, measurementID=, dataSize=20, dataType=VECTOR, compressionType=LZ4, encodingType=TS_2DIFF, size=6
2235| [Page Header] HeaderSize:2, UncompressedSize:24, CompressedSize:18
2237| [Page Data] Size:18
2255| [Chunk] of root.a.b.c.d2.s0, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
2255| [Chunk Header] marker=69, measurementID=s0, dataSize=415, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
2264| [Page Header] HeaderSize:4, UncompressedSize:411, CompressedSize:411
2268| [Page Data] Size:411
2679| [Chunk] of root.a.b.c.d2.s1, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
2679| [Chunk Header] marker=69, measurementID=s1, dataSize=415, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
2688| [Page Header] HeaderSize:4, UncompressedSize:411, CompressedSize:411
2692| [Page Data] Size:411
3103| [Chunk] of root.a.b.c.d2.s2, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
3103| [Chunk Header] marker=69, measurementID=s2, dataSize=415, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
3112| [Page Header] HeaderSize:4, UncompressedSize:411, CompressedSize:411
3116| [Page Data] Size:411
3527| [Chunk] of root.a.b.c.d2.s3, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
3527| [Chunk Header] marker=69, measurementID=s3, dataSize=415, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
3536| [Page Header] HeaderSize:4, UncompressedSize:411, CompressedSize:411
3540| [Page Data] Size:411
3951| [Chunk] of root.a.b.c.d2.s4, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
3951| [Chunk Header] marker=69, measurementID=s4, dataSize=415, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=9
3960| [Page Header] HeaderSize:4, UncompressedSize:411, CompressedSize:411
3964| [Page Data] Size:411
||||||||||||||||||||| [ChunkGroup] of root.a.b.c.d2 ends
||||||||||||||||||||| [ChunkGroup] of testTable0.0.0.0.0.0, num of Chunks:6
4375| [ChunkGroup Header]
4375| [marker] 0
4376| [deviceID] testTable0.0.0.0.0.0 size=43
4398| [Chunk] of testTable0.0.0.0.0.0., startTime: 0 endTime: 0 count: 1
4398| [Chunk Header] marker=-123, measurementID=, dataSize=20, dataType=VECTOR, compressionType=LZ4, encodingType=TS_2DIFF, size=6
4404| [Page Header] HeaderSize:2, UncompressedSize:24, CompressedSize:18
4406| [Page Data] Size:18
4424| [Chunk] of testTable0.0.0.0.0.0.s0, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
4424| [Chunk Header] marker=69, measurementID=s0, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4432| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4434| [Page Data] Size:13
4447| [Chunk] of testTable0.0.0.0.0.0.s1, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
4447| [Chunk Header] marker=69, measurementID=s1, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4455| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4457| [Page Data] Size:13
4470| [Chunk] of testTable0.0.0.0.0.0.s2, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
4470| [Chunk Header] marker=69, measurementID=s2, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4478| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4480| [Page Data] Size:13
4493| [Chunk] of testTable0.0.0.0.0.0.s3, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
4493| [Chunk Header] marker=69, measurementID=s3, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4501| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4503| [Page Data] Size:13
4516| [Chunk] of testTable0.0.0.0.0.0.s4, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
4516| [Chunk Header] marker=69, measurementID=s4, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4524| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4526| [Page Data] Size:13
||||||||||||||||||||| [ChunkGroup] of testTable0.0.0.0.0.0 ends
||||||||||||||||||||| [ChunkGroup] of testTable0.1.1.1.1.1, num of Chunks:6
4539| [ChunkGroup Header]
4539| [marker] 0
4540| [deviceID] testTable0.1.1.1.1.1 size=43
4562| [Chunk] of testTable0.1.1.1.1.1., startTime: 1 endTime: 1 count: 1
4562| [Chunk Header] marker=-123, measurementID=, dataSize=20, dataType=VECTOR, compressionType=LZ4, encodingType=TS_2DIFF, size=6
4568| [Page Header] HeaderSize:2, UncompressedSize:24, CompressedSize:18
4570| [Page Data] Size:18
4588| [Chunk] of testTable0.1.1.1.1.1.s0, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
4588| [Chunk Header] marker=69, measurementID=s0, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4596| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4598| [Page Data] Size:13
4611| [Chunk] of testTable0.1.1.1.1.1.s1, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
4611| [Chunk Header] marker=69, measurementID=s1, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4619| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4621| [Page Data] Size:13
4634| [Chunk] of testTable0.1.1.1.1.1.s2, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
4634| [Chunk Header] marker=69, measurementID=s2, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4642| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4644| [Page Data] Size:13
4657| [Chunk] of testTable0.1.1.1.1.1.s3, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
4657| [Chunk Header] marker=69, measurementID=s3, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4665| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4667| [Page Data] Size:13
4680| [Chunk] of testTable0.1.1.1.1.1.s4, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
4680| [Chunk Header] marker=69, measurementID=s4, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4688| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4690| [Page Data] Size:13
||||||||||||||||||||| [ChunkGroup] of testTable0.1.1.1.1.1 ends
||||||||||||||||||||| [ChunkGroup] of testTable0.2.2.2.2.2, num of Chunks:6
4703| [ChunkGroup Header]
4703| [marker] 0
4704| [deviceID] testTable0.2.2.2.2.2 size=43
4726| [Chunk] of testTable0.2.2.2.2.2., startTime: 2 endTime: 2 count: 1
4726| [Chunk Header] marker=-123, measurementID=, dataSize=20, dataType=VECTOR, compressionType=LZ4, encodingType=TS_2DIFF, size=6
4732| [Page Header] HeaderSize:2, UncompressedSize:24, CompressedSize:18
4734| [Page Data] Size:18
4752| [Chunk] of testTable0.2.2.2.2.2.s0, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
4752| [Chunk Header] marker=69, measurementID=s0, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4760| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4762| [Page Data] Size:13
4775| [Chunk] of testTable0.2.2.2.2.2.s1, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
4775| [Chunk Header] marker=69, measurementID=s1, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4783| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4785| [Page Data] Size:13
4798| [Chunk] of testTable0.2.2.2.2.2.s2, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
4798| [Chunk Header] marker=69, measurementID=s2, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4806| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4808| [Page Data] Size:13
4821| [Chunk] of testTable0.2.2.2.2.2.s3, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
4821| [Chunk Header] marker=69, measurementID=s3, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4829| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4831| [Page Data] Size:13
4844| [Chunk] of testTable0.2.2.2.2.2.s4, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
4844| [Chunk Header] marker=69, measurementID=s4, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4852| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4854| [Page Data] Size:13
||||||||||||||||||||| [ChunkGroup] of testTable0.2.2.2.2.2 ends
||||||||||||||||||||| [ChunkGroup] of testTable0.3.3.3.3.3, num of Chunks:6
4867| [ChunkGroup Header]
4867| [marker] 0
4868| [deviceID] testTable0.3.3.3.3.3 size=43
4890| [Chunk] of testTable0.3.3.3.3.3., startTime: 3 endTime: 3 count: 1
4890| [Chunk Header] marker=-123, measurementID=, dataSize=20, dataType=VECTOR, compressionType=LZ4, encodingType=TS_2DIFF, size=6
4896| [Page Header] HeaderSize:2, UncompressedSize:24, CompressedSize:18
4898| [Page Data] Size:18
4916| [Chunk] of testTable0.3.3.3.3.3.s0, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
4916| [Chunk Header] marker=69, measurementID=s0, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4924| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4926| [Page Data] Size:13
4939| [Chunk] of testTable0.3.3.3.3.3.s1, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
4939| [Chunk Header] marker=69, measurementID=s1, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4947| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4949| [Page Data] Size:13
4962| [Chunk] of testTable0.3.3.3.3.3.s2, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
4962| [Chunk Header] marker=69, measurementID=s2, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4970| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4972| [Page Data] Size:13
4985| [Chunk] of testTable0.3.3.3.3.3.s3, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
4985| [Chunk Header] marker=69, measurementID=s3, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
4993| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
4995| [Page Data] Size:13
5008| [Chunk] of testTable0.3.3.3.3.3.s4, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
5008| [Chunk Header] marker=69, measurementID=s4, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
5016| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
5018| [Page Data] Size:13
||||||||||||||||||||| [ChunkGroup] of testTable0.3.3.3.3.3 ends
||||||||||||||||||||| [ChunkGroup] of testTable0.4.4.4.4.4, num of Chunks:6
5031| [ChunkGroup Header]
5031| [marker] 0
5032| [deviceID] testTable0.4.4.4.4.4 size=43
5054| [Chunk] of testTable0.4.4.4.4.4., startTime: 4 endTime: 4 count: 1
5054| [Chunk Header] marker=-123, measurementID=, dataSize=20, dataType=VECTOR, compressionType=LZ4, encodingType=TS_2DIFF, size=6
5060| [Page Header] HeaderSize:2, UncompressedSize:24, CompressedSize:18
5062| [Page Data] Size:18
5080| [Chunk] of testTable0.4.4.4.4.4.s0, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
5080| [Chunk Header] marker=69, measurementID=s0, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
5088| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
5090| [Page Data] Size:13
5103| [Chunk] of testTable0.4.4.4.4.4.s1, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
5103| [Chunk Header] marker=69, measurementID=s1, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
5111| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
5113| [Page Data] Size:13
5126| [Chunk] of testTable0.4.4.4.4.4.s2, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
5126| [Chunk Header] marker=69, measurementID=s2, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
5134| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
5136| [Page Data] Size:13
5149| [Chunk] of testTable0.4.4.4.4.4.s3, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
5149| [Chunk Header] marker=69, measurementID=s3, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
5157| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
5159| [Page Data] Size:13
5172| [Chunk] of testTable0.4.4.4.4.4.s4, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
5172| [Chunk Header] marker=69, measurementID=s4, dataSize=15, dataType=INT64, compressionType=UNCOMPRESSED, encodingType=PLAIN, size=8
5180| [Page Header] HeaderSize:2, UncompressedSize:13, CompressedSize:13
5182| [Page Data] Size:13
||||||||||||||||||||| [ChunkGroup] of testTable0.4.4.4.4.4 ends
5195| [marker] 2
5196| [TimeseriesMetadata] of root.a.b.c.d1.s0, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5259| [ChunkMetadata] offset=23, size=8
5267| [TimeseriesMetadata] of root.a.b.c.d1.s1, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5330| [ChunkMetadata] offset=461, size=8
5338| [TimeseriesMetadata] of root.a.b.c.d1.s2, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5401| [ChunkMetadata] offset=899, size=8
5409| [TimeseriesMetadata] of root.a.b.c.d1.s3, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5472| [ChunkMetadata] offset=1337, size=8
5480| [TimeseriesMetadata] of root.a.b.c.d1.s4, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5543| [ChunkMetadata] offset=1775, size=8
5551| [TimeseriesMetadata] of root.a.b.c.d2., tsDataType:VECTOR, sizeWithoutChunkMetadata:21, startTime: 0 endTime: 49 count: 50
5572| [ChunkMetadata] offset=2229, size=8
5580| [TimeseriesMetadata] of root.a.b.c.d2.s0, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5643| [ChunkMetadata] offset=2255, size=8
5651| [TimeseriesMetadata] of root.a.b.c.d2.s1, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5714| [ChunkMetadata] offset=2679, size=8
5722| [TimeseriesMetadata] of root.a.b.c.d2.s2, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5785| [ChunkMetadata] offset=3103, size=8
5793| [TimeseriesMetadata] of root.a.b.c.d2.s3, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5856| [ChunkMetadata] offset=3527, size=8
5864| [TimeseriesMetadata] of root.a.b.c.d2.s4, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 49 count: 50 [minValue:0,maxValue:49,firstValue:0,lastValue:49,sumValue:1225.0]
5927| [ChunkMetadata] offset=3951, size=8
5935| [TimeseriesMetadata] of testTable0.0.0.0.0.0., tsDataType:VECTOR, sizeWithoutChunkMetadata:21, startTime: 0 endTime: 0 count: 1
5956| [ChunkMetadata] offset=4398, size=8
5964| [TimeseriesMetadata] of testTable0.0.0.0.0.0.s0, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
6027| [ChunkMetadata] offset=4424, size=8
6035| [TimeseriesMetadata] of testTable0.0.0.0.0.0.s1, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
6098| [ChunkMetadata] offset=4447, size=8
6106| [TimeseriesMetadata] of testTable0.0.0.0.0.0.s2, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
6169| [ChunkMetadata] offset=4470, size=8
6177| [TimeseriesMetadata] of testTable0.0.0.0.0.0.s3, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
6240| [ChunkMetadata] offset=4493, size=8
6248| [TimeseriesMetadata] of testTable0.0.0.0.0.0.s4, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 0 endTime: 0 count: 1 [minValue:0,maxValue:0,firstValue:0,lastValue:0,sumValue:0.0]
6311| [ChunkMetadata] offset=4516, size=8
6319| [TimeseriesMetadata] of testTable0.1.1.1.1.1., tsDataType:VECTOR, sizeWithoutChunkMetadata:21, startTime: 1 endTime: 1 count: 1
6340| [ChunkMetadata] offset=4562, size=8
6348| [TimeseriesMetadata] of testTable0.1.1.1.1.1.s0, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
6411| [ChunkMetadata] offset=4588, size=8
6419| [TimeseriesMetadata] of testTable0.1.1.1.1.1.s1, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
6482| [ChunkMetadata] offset=4611, size=8
6490| [TimeseriesMetadata] of testTable0.1.1.1.1.1.s2, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
6553| [ChunkMetadata] offset=4634, size=8
6561| [TimeseriesMetadata] of testTable0.1.1.1.1.1.s3, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
6624| [ChunkMetadata] offset=4657, size=8
6632| [TimeseriesMetadata] of testTable0.1.1.1.1.1.s4, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 1 endTime: 1 count: 1 [minValue:1,maxValue:1,firstValue:1,lastValue:1,sumValue:1.0]
6695| [ChunkMetadata] offset=4680, size=8
6703| [TimeseriesMetadata] of testTable0.2.2.2.2.2., tsDataType:VECTOR, sizeWithoutChunkMetadata:21, startTime: 2 endTime: 2 count: 1
6724| [ChunkMetadata] offset=4726, size=8
6732| [TimeseriesMetadata] of testTable0.2.2.2.2.2.s0, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
6795| [ChunkMetadata] offset=4752, size=8
6803| [TimeseriesMetadata] of testTable0.2.2.2.2.2.s1, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
6866| [ChunkMetadata] offset=4775, size=8
6874| [TimeseriesMetadata] of testTable0.2.2.2.2.2.s2, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
6937| [ChunkMetadata] offset=4798, size=8
6945| [TimeseriesMetadata] of testTable0.2.2.2.2.2.s3, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
7008| [ChunkMetadata] offset=4821, size=8
7016| [TimeseriesMetadata] of testTable0.2.2.2.2.2.s4, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 2 endTime: 2 count: 1 [minValue:2,maxValue:2,firstValue:2,lastValue:2,sumValue:2.0]
7079| [ChunkMetadata] offset=4844, size=8
7087| [TimeseriesMetadata] of testTable0.3.3.3.3.3., tsDataType:VECTOR, sizeWithoutChunkMetadata:21, startTime: 3 endTime: 3 count: 1
7108| [ChunkMetadata] offset=4890, size=8
7116| [TimeseriesMetadata] of testTable0.3.3.3.3.3.s0, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
7179| [ChunkMetadata] offset=4916, size=8
7187| [TimeseriesMetadata] of testTable0.3.3.3.3.3.s1, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
7250| [ChunkMetadata] offset=4939, size=8
7258| [TimeseriesMetadata] of testTable0.3.3.3.3.3.s2, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
7321| [ChunkMetadata] offset=4962, size=8
7329| [TimeseriesMetadata] of testTable0.3.3.3.3.3.s3, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
7392| [ChunkMetadata] offset=4985, size=8
7400| [TimeseriesMetadata] of testTable0.3.3.3.3.3.s4, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 3 endTime: 3 count: 1 [minValue:3,maxValue:3,firstValue:3,lastValue:3,sumValue:3.0]
7463| [ChunkMetadata] offset=5008, size=8
7471| [TimeseriesMetadata] of testTable0.4.4.4.4.4., tsDataType:VECTOR, sizeWithoutChunkMetadata:21, startTime: 4 endTime: 4 count: 1
7492| [ChunkMetadata] offset=5054, size=8
7500| [TimeseriesMetadata] of testTable0.4.4.4.4.4.s0, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
7563| [ChunkMetadata] offset=5080, size=8
7571| [TimeseriesMetadata] of testTable0.4.4.4.4.4.s1, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
7634| [ChunkMetadata] offset=5103, size=8
7642| [TimeseriesMetadata] of testTable0.4.4.4.4.4.s2, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
7705| [ChunkMetadata] offset=5126, size=8
7713| [TimeseriesMetadata] of testTable0.4.4.4.4.4.s3, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
7776| [ChunkMetadata] offset=5149, size=8
7784| [TimeseriesMetadata] of testTable0.4.4.4.4.4.s4, tsDataType:INT64, sizeWithoutChunkMetadata:63, startTime: 4 endTime: 4 count: 1 [minValue:4,maxValue:4,firstValue:4,lastValue:4,sumValue:4.0]
7847| [ChunkMetadata] offset=5172, size=8
7855| [MetadataIndexNode]
7855| childrenCnt=1
7856| <s0, 5196>
7867| endOffset=5551
7875| nodeType=LEAF_MEASUREMENT
7876| [MetadataIndexNode]
7876| childrenCnt=1
7877| <, 5551>
7886| endOffset=5935
7894| nodeType=LEAF_MEASUREMENT
7895| [MetadataIndexNode]
7895| childrenCnt=1
7896| <, 5935>
7905| endOffset=6319
7913| nodeType=LEAF_MEASUREMENT
7914| [MetadataIndexNode]
7914| childrenCnt=1
7915| <, 6319>
7924| endOffset=6703
7932| nodeType=LEAF_MEASUREMENT
7933| [MetadataIndexNode]
7933| childrenCnt=1
7934| <, 6703>
7943| endOffset=7087
7951| nodeType=LEAF_MEASUREMENT
7952| [MetadataIndexNode]
7952| childrenCnt=1
7953| <, 7087>
7962| endOffset=7471
7970| nodeType=LEAF_MEASUREMENT
7971| [MetadataIndexNode]
7971| childrenCnt=1
7972| <, 7471>
7981| endOffset=7855
7989| nodeType=LEAF_MEASUREMENT
||||||||||||||||||||| [TsFileMetadata] begins
7990| TableIndexRootCnt=2
7994| [Table Name] root.a.b, size=12
8006| [MetadataIndexNode]
8006| childrenCnt=2
8007| <root.a.b.c.d1, 7855>
8042| <root.a.b.c.d2, 7876>
8077| endOffset=7895
8085| nodeType=LEAF_DEVICE
8086| [Table Name] testTable0, size=14
8100| [MetadataIndexNode]
8100| childrenCnt=5
8101| <testTable0.0.0.0.0.0, 7895>
8152| <testTable0.1.1.1.1.1, 7914>
8203| <testTable0.2.2.2.2.2, 7933>
8254| <testTable0.3.3.3.3.3, 7952>
8305| <testTable0.4.4.4.4.4, 7971>
8356| endOffset=7990
8364| nodeType=LEAF_DEVICE
8365| TableSchemaCnt=2
8369| [TableSchema] TableSchema{tableName='root.a.b', columnSchemas=[[s0,INT64,PLAIN,,UNCOMPRESSED], [s1,INT64,PLAIN,,UNCOMPRESSED], [s2,INT64,PLAIN,,UNCOMPRESSED], [s3,INT64,PLAIN,,UNCOMPRESSED], [s4,INT64,PLAIN,,UNCOMPRESSED], [,VECTOR,TS_2DIFF,,LZ4]], columnTypes=[MEASUREMENT, MEASUREMENT, MEASUREMENT, MEASUREMENT, MEASUREMENT, MEASUREMENT]}, size=101
8482| [TableSchema] TableSchema{tableName='testTable0', columnSchemas=[[id0,TEXT,PLAIN,,UNCOMPRESSED], [id1,TEXT,PLAIN,,UNCOMPRESSED], [id2,TEXT,PLAIN,,UNCOMPRESSED], [id3,TEXT,PLAIN,,UNCOMPRESSED], [id4,TEXT,PLAIN,,UNCOMPRESSED], [s0,INT64,PLAIN,,UNCOMPRESSED], [s1,INT64,PLAIN,,UNCOMPRESSED], [s2,INT64,PLAIN,,UNCOMPRESSED], [s3,INT64,PLAIN,,UNCOMPRESSED], [s4,INT64,PLAIN,,UNCOMPRESSED]], columnTypes=[ID, ID, ID, ID, ID, MEASUREMENT, MEASUREMENT, MEASUREMENT, MEASUREMENT, MEASUREMENT]}, size=176
8672| [Meta Offset] 5195
8680| [Bloom Filter Size] bit vector byte array length=325
8684| [Bloom Filter] , filterCapacity=256, hashFunctionSize=5
||||||||||||||||||||| [TsFileMetadata] ends
8569| [TsFileMetadataSize] 579
8573| [magic tail] TsFile
8579| END of TsFile
---------------------------- IndexOfTimerseriesIndex Tree -----------------------------
root.a.b
[MetadataIndex:LEAF_DEVICE]
└──────[root.a.b.c.d1,7855]
[MetadataIndex:LEAF_MEASUREMENT]
└──────[s0,5196]
└──────[root.a.b.c.d2,7876]
[MetadataIndex:LEAF_MEASUREMENT]
└──────[,5551]
testTable0
[MetadataIndex:LEAF_DEVICE]
└──────[testTable0.0.0.0.0.0,7895]
[MetadataIndex:LEAF_MEASUREMENT]
└──────[,5935]
└──────[testTable0.1.1.1.1.1,7914]
[MetadataIndex:LEAF_MEASUREMENT]
└──────[,6319]
└──────[testTable0.2.2.2.2.2,7933]
[MetadataIndex:LEAF_MEASUREMENT]
└──────[,6703]
└──────[testTable0.3.3.3.3.3,7952]
[MetadataIndex:LEAF_MEASUREMENT]
└──────[,7087]
└──────[testTable0.4.4.4.4.4,7971]
[MetadataIndex:LEAF_MEASUREMENT]
└──────[,7471] 10. V5 展望
10.1 动机
TsFile中的数据可以被视作为由时间戳、设备、测点三个维度所构成三维矩阵，图10.1展示了这一点。TsFile将不同设备的数据存储在不同的数据块（ChunkGroup）中，在图9.1中，相同颜色的数据被存储在同一个ChunkGroup中，每一列（连同对应的时间戳）被存储于一个Chunk中。在理想状态下，即时间戳的个数远大于设备的个数，每个Chunk具有较多的数据点。这使得：一方面，TsFile所应用的压缩编码算法可以产生比较好的效果；另一方面，设备的数据局部性较强，针对设备的查询也可以以比较高效的方式执行。
暂时无法在飞书文档外展示此内容
图10.1 理想状态下的TsFile V4中数据的逻辑视图
然而，在某些场景下，特别是近端侧场景，写出的TsFile只包含极少的时间戳。例如一个场站将其管辖的设备在一次采集中产生的数据写为一个TsFile，并将该TsFile上传至云端。该TsFile就可能只包含一个时间戳，但是包含大量的设备。图10.2给出了类似场景下的一个例子。显然，压缩算法无法在这样的场景下找到和去除冗余信息，其结果是极低的存储效率和存储效率。
暂时无法在飞书文档外展示此内容
图10.2 极端状态下的TsFile V4中数据的逻辑视图
对此，图10.3提出了将按 Device 组织ChunkGroup的方式换为在一个ChunkGroup中包含多个 Device 的解决办法。在该方法中，每个ChunkGroup包含若干个设备的一组测点。数据在其中以先设备后时间的方式排序，以便按照设备进行过滤和进行单设备查询。相较于图10.2，图10.3虽然看似重复存储了大量的相同时间戳，但是这些时间戳很容易被压缩，并且包含时间戳的数据块从10000个降低到了1个，时间戳对空间的总占用预期将减少；另外，这样的存储方式得到了对测点值进行压缩的可能，尽管这些测点并不来自于同一条时间序列，但是它们仍来自同一类被测量对象（比如都是温度），因此可以通过相同的值域等特征来得到一定的压缩。
暂时无法在飞书文档外展示此内容
图10.3 TsFile V5 中数据的逻辑视图
该存储方式所要解决的主要问题包含：

1. 如何尽可能减小重复存储设备标识所带来的额外开销；
2. 如何针对新结构构建索引并最大程度复用之前的逻辑；
3. 如何提供统一的查询接口，使得上层不感知具体的存储方式。
   10.2 文件结构
   10.2.1 整体描述
   图10.4给出了V5 的存储结构图。对比 V4，转置 TsFile 使用FusedChunkGroupHeader替代原有的ChunkGroupHeader、FusedChunk替换Chunk、FusedPage替换Page、LEAF_FUSED_DEVICE替换LEAF_DEVICE。此外，TsFileMetadata 中也有新增字段。当总体的结构顺序大体一致，因此可以在很大程度上复用 V4 的读写流程。具体的结构变化将在下一个小节介绍。
   暂时无法在飞书文档外展示此内容
   图10.4 TsFile V5 存储结构图
   10.2.2 结构详解
   10.2.2.1 数据区
   数据区中的TimePage、ValuePage、TimeChunk、ValueChunk等结构和V4一致，这里不再赘述。本节主要介绍发生新引入或发生变化的DevicePage、DeviceChunk和FusedChunk。
   10.2.2.1.1 DevicePage
   一个DevicePage存储了图10.3中Device列的一部分。它采用游程编码对使用字典编码后的设备标识进行编码，并使用LZ4算法对编码结果进行压缩。它包含一个DevicePageHeader和压缩后的数据。
   表 10.1 DevicePageHeader 成员表
   成员
   类型
   解释
   size
   int
   压缩后的数据大小
   minDeviceCode
   int
   字典编码后的设备标识的最小值
   maxDeviceCode
   int
   字典编码后的设备标识的最大值
   10.2.2.1.2 DeviceChunk
   一个DeviceChunk存储了图10.3中完整的Device列。它由一个ChunkHeader和后续若干个DevicePage组成。在ChunkHeader中使用chunkType的第6位来标记该Chunk为DeviceChunk。
   10.2.2.1.3 FusedChunk
   一个FusedChunk存储了若干个测点一段时间的数据，FusedChunk内数据先按设备编号排序，再按时间排序。它由一个DeviceChunk，一个TimeChunk和若干个ValueChunk构成。
   10.2.2.1.3.1 FusedChunkGroup
   FusedChunkGroup存储了多个设备实体下一个测点在一段时间的数据。由一个FusedChunkGroupHeader、若干个FusedChunk和一个字节的分隔符0x06组成。如表10.1所示，FusedChunkGroupHeader包含以下成员。
   为了减小在FusedChunk中重复存储设备标识的影响，转置 TsFile 对设备标识进行了字典编码，字典编码的结果存储在该设备第一次出现的FusedChunkGroup的 Header 中。
   表 10.1 FusedChunkGroupHeader 成员表
   成员
   类型
   解释
   dataSize
   int
   该 FusedChunkGroup 的Chunk 大小之和
   deviceIdDict
   Map<IDeviceID, Integer>
   第一次出现是在该 TransChunkGroup 的设备及其设备编号
   10.2.2.2 索引区
   10.2.2.2.1 MetadataIndexNode

- MetaIndexNode
  MetadataIndexNode的结构和 V4 相同，但是多出一种节点类型 LEAF_FUSED_DEVICE。该类节点使用设备编码构成的若干个区间作为子节点指针。例如，如果编号为1、2、3、4、5、10、11、12的设备都具有“s1”和“s2”两个测点，那么将有一个LEAF_FUSED_DEVICE节点使用键为“[1-5,10-12]”的指针指向一个LEAF_MEASUREMENT，该LEAF_MEASUREMENT包含一个指针，以“”为键指向存有这些设备的设备列的TimeseriesMetadata，时间列和其余值列可以通过从该TimeseriesMetadata线性搜索找到。
- IMetaIndexEntry
  对应LEAF_FUSED_DEVICE节点，将有一类新的IMetaIndexEntry，即 FusedDeviceMetadataIndexEntry。
  表 10.2 FusedDeviceMetadataIndexEntry 成员表
  成员
  类型
  解释
  deviceNums
  List<Pair<Integer, Integer>>
  以设备编号的若干区间表示的该子节点所涉及的设备
  offset
  long
  子节点开始偏移量
  10.2.2.2.2 FileMetadata
  转置 TsFile 的FileMetadata相较于V4额外记录了设备字典的位置，即那些包含有设备字典的FusedChunkGroupHeader的文件偏移量列表。
  10.3 写入流程
  10.3.1 注册元数据
  V5 TsFile 的元数据注册和 V4 基本相同。在使用V5的特性进行写入前需要创建FusionSet（即Set<IDeviceID>和对应的ChunkGroupWriter），即哪些设备需要放在同一个ChunkGroup中。在TsFileWriter中使用一个Map<String, Map<IDeviceID, Set<IDeviceID>>> fusionSets维护每个表的IDeviceID和FusionSet的映射关系。创建FusionSet的函数形如void createFusionSet(List<IDeviceID> deviceIds)，其过程如下：

1. 遍历deviceIds中的每一个IDeviceID，记为dId；
1. 从fusionSets中找到dId对应的FusionSet fusionSet；
1. 如果fusionSet中已经有ChunkGroupWriter，将dId标记为hasFusionSet；
1. 如果fusionSet中尚没有ChunkGroupWriter，将fusionSet加入到临时变量Set<FusionSet> involvedFusionSet中，将dId标记为hasFusionSet；
1. 如果 involvedFusionSet中不存在任何FusionSet，将所有未标记为hasFusionSet的dId创建为一个FusionSet并为其中的每个IDeviceID加入到fusionSets中；
1. 如果involvedFusionSet中存在FusionSet，将这些FusionSet合并为一个，将所有将所有未标记为hasFusionSet的dId加入到这个FusionSet中，并为其中的每个IDeviceID加入到fusionSets中。
   10.3.2 TsFileWriter写入数据
   V5 TsFile 在V4 提供的写入接口的基础上，额外提供写入List<TsRecord>和List<Tablet>的接口，这些接口会主动尝试为写入的一批设备创建FusionSet。写入一个设备时，首先将尝试寻找该设备对应的FusionSet，如果无法找到，则按照V4的流程进行写入；如果可以找到，则为该FusionSet创建一个FusedChunkGroupWriterImpl，其写入流程如下：
1. 对当前写入的设备进行字典编码得到一个编号或者取得之前已经编好的设备编号；
1. 内存中存在两个设备字典（即Map<IDeviceID, Integer>），currentChunkGroupDeviceDict和previousDeviceDict；
1. 先在previousDeviceDict中进行查询，如果没有查到再在currentChunkGroupDeviceDict中查询或者为设备编码；
1. 为写入的每一列创建ValueChunkWriter或者检查当前写入的数据与之前写入的数据类型是否一致，如果不一致则抛出异常；
1. 分别使用DeviceChunkWriter（即固定为int类型，RLE编码和LZ4压缩的ChunkWriter）、TimeChunkWriter、ValueChunkWriter的将设备编号-时间-值三者写入；
1. 检查当前是否有某个Page写满了，如果是，就执行第5步，否则这次写入结束；
1. 将当前各PageWriter的statistic合并到对应ChunkWriter的statistic，然后为当前Page生成PageHeader，将这批数据压缩成一为PageData，把PageHeader、PageData写入Chunk中。
   10.3.3 数据刷盘
   这里只介绍FusedChunkGroup的相关过程。
1. TsFileIOWriter执行startFile()
   向TsFileIOWriter的TsFileOutput写入字符串"TsFile"和版本号；
1. TsFileIOWriter执行startChunkGroup()
1. 向TsFileIOWriter的TsFileOutput写入CHUNK_GROUP_HEADER_MARKER；
1. 将写入过程中维护的设备字典currentChunkGroupDeviceDict写出到TsFileOutput，将currentChunkGroupDeviceDict合并到previousDeviceDict，将currentChunkGroupDeviceDict清空，并将该ChunkGroupHeader的偏移量记录在FileMetadata的chunkGroupHeaderWithDict字段；
1. 为该FusionSet创建一个FusedChunkGroupMetadata对象，存入到List<FusedChunkGroupMetadata> fusedChunkGrouplist中；
1. FusedChunkGroupWriter执行flushToFileWriter()
1. 遍历该ChunkGroup下的DeviceChunkWriter、TimeChunkWriter和每个测点的ValueChunkWriter；
1. 调用ChunkWriter的sealCurrentPage()对该Chunk的最后一个Page进行封口，同时将所包含的Page数量和dataSize写入ChunkHeader。
1. 调用TsFileIOWriter的startFlushChunk()，生成当前Chunk的ChunkMetadata以及ChunkHeader。然后把ChunkHeader写入TsFileIOWriter的TsFileOutput中。
1. 调用TsFileIOWriter的writeBytesToStream()，先将Chunk的chunkData写到TsFileOutput中，然后刷到磁盘;
1. 调用TsFileIOWriter的endFlushChunk()，将3.c.生成的ChunkMetadata插入到ChunkGroupMetadata中。
   不同的FusedChunkGroup通过一字节分隔符6区分。同一ChunkGroup下的不同Chunk通过一字节分隔符区分，该分隔符第一位为1，如果只有一页，第三位为1；如果为Device列，第六位为1；如果为Value列，第七位为1；如果为Time列，第八位为1。
1. TsFileIOWriter执行endChunkGroup()
1. 将2.b.生成的ChunkGroupMetadata插入到 chunkGroupMetadataList中;
1. 如果该ChunkGroupMetadata为树上的 Device，在Schema中为对应的逻辑表更新列信息;
1. 如果Schema中没有该 Device 对应的表的TableSchema，为其创建一个LogicalTableSchema并放入到Schema中；
1. 使用该 Device 的 DeviceId 对应的层数更新LogicalTableSchema中的maxLevel（即取最大值）；
1. 遍历该ChunkGroupMetadata的ChunkMetadata
1. 如果LogicalTableSchema中不存在对应的MeasurementSchema，则将其添加到LogicalTableSchema中；
1. 如果已存在对应的MeasurementSchema，但是其中的数据类型与ChunkMetadata中的不一致，则将该MeasurementSchema中的数据类型设置为TEXT。
   10.3.4 建立索引并结束文件
   主要用到TsFileIOWriter这个类。
   根据内存中缓存的元数据，即上一小节2.c.生成的ChunkGroupMetadata和3.c.生成的ChunkMetadata，生成TsFileMetadata并追加到文件尾部，最后关闭文件。
   10.3.4.1 作用：
   生成TsFileMetadata的过程中的关键一步是建立元数据索引 (MetadataIndex) 树。元数据索引采用树形结构进行设计的目的是在设备数或者测点数量过大时，可以不用一次读取所有的TimeseriesMetadata，只需要根据所读取的传感器定位对应的节点，从而减少 I/O，加快查询速度。
   10.3.4.2 流程：
   TsFile 的索引区以自底向上的方式构建。
1. 每一个FusionSet在刷盘时会对应生成一个 FusedChunkGroupMetadata，这些 FusedChunkGroupMetadata 被缓存在内存中或是暂存在另一个文件中；
1. 对于每一个FusionSet，它所对应的所有FusedChunkGroupMetadata 被汇总，然后其中每一列对应的ChunkMetadata记录在一个 TimeseriesMetadata 中；
1. 对于每一个FusionSet，属于该FusionSet的 TimeseriesMetadata 按照一定数量（该值记为 maxDegree）进行分组，每一组记录在一个类型为 LEAF_MEASUREMENT 的 MetadataIndexNode 父节点中；在此步骤中，每生成一个 MetadataIndexNode，在其中记录文件当前偏移量, 并将其包含的 TimeseriesMetadata 写到文件中；
1. 对于某设备生成的类型为LEAF_MEASUREMENT 的 MetadataIndexNode，如果其个数超过一，同样按照 maxDegree 分组构造类型为 INTERNAL_MEASUREMENT 的 MetadataIndexNode 父节点；并对这些父节点递归地构造类型为 INTERNAL_MEASUREMENT 的 MetadataIndexNode父节点，直到构造出一个根节点；在此步骤中，每生成一个 MetadataIndexNode，在其中记录文件当前偏移量，将其包含的子节点写到文件中；
1. 对于所有设备生成的构造类型为INTERNAL_MEASUREMENT 的 MetadataIndexNode根节点，按照设备所属的表（对于树模型设备，使用数据库名作为临时表名）进行分组；
1. 对于每张表下的设备，仿照第3、4步为其构造类型为 LEAF_FUSED_DEVICE 和 INTERNAL_FUSED_DEVICE 的 MetadataIndexNode；最终每个表有一个MetadataIndexNode根节点；
1. 将每张表的 MetadataIndexNode 根节点写入到文件，并将其偏移量记录到一个 TsFileMetadata 中；
1. 对于未包含在FusionSet中的设备，会以 V4 对应的第1~7步，生成对应的索引根节点；因此，每个表将可能存在两个索引根节点，一个对应不在FusionSet中的设备，一个对应在FusionSet中的设备；
1. 将每张表的 TableSchema 记录到 TsFileMetadata中，最后将 TsFileMetadata 写入到文件中；
1. 对于LogicTableSchema，它会根据此时的maxLevel，生成对应个数的标识列信息（第i列列名为 “\_\_Leveli”，类型均为 Text）加到数据刷盘4.b.iii步所生成的测点列MeasurementSchema之前；这些生成的标识列与测点列合并作为最终的列信息；
1. TsFileMetadata中包含所有包含设备字典所在位置的chunkGroupHeaderWithDict。
   10.4 查询流程
   V5 的查询流程与V4总体一致，但在以下方面会出现变化：
1. 读取 TsFileMetadata 后，会根据其中的chunkGroupHeaderWithDict字段，读取其中的ChunkGroupHeader，并从中恢复出设备字典；
1. 对索引树进行查询时，如果待查询的设备位于设备字典中，则查询FusionSet对应的索引节点，否则查询普通索引节点；
1. 查询到具体FusedChunk后，自动根据设备编号构造于设备列上的过滤条件并应用。

附录

1.  编码算法
    1.1 TS_2DIFF编码算法
    1.1.1 简介
    TS_2DIFF不是二阶差分，只是一阶差分完进行了一次+base来去除负数。二阶差分适合线性数据，线性数据二阶之后就是0了，例如定时写数据的时间戳。
    1.1.2 原理
    差分编码，又称增量编码，是以序列式资料之间的差异储存或传送资料的方式（相对于储存传送完整档案的方式）。在需要档案改变历史的情况下的差分编码有时又称为差分压缩。差异储存在称为“delta”或“diff”的不连续档案中。由于改变通常很小（平均占全部大小的2%），差分编码能大幅减少资料的重复。一连串独特的delta档案在空间上要比未编码的相等档案有效率多了。
    比较适合编码单调递增或者递减的序列数据。例如 2,4,4,6,8 , Delta编码后为 2,2,0,2,2 ，再 Delta 编码后为 2,0,-2,2,0。通常其也会搭配 RLE、Simple8b 或者 Zig-zag 一起使用。
    [图片]
    1.1.3 适用数据类型
    INT32，INT64，FLOAT，DOUBLE
    1.1.4 适用数据模式
    二阶差分编码，比较适合编码单调递增或者递减的序列数据，不适合编码波动较大的数据
    二阶差分编码（TS_2DIFF）对 float 和 double 的编码是有精度限制的，默认保留 2 位小数，对精度要求较高的数据不建议TS_2DIFF，推荐使用 GORILLA。

1.2 GORILLA编码算法
1.2.1 简介
Gorilla是Facebook在2015年在VLDB发表的论文Gorilla: A Fast, Scalable, In-Memory Time Series Database
中介绍的内存型时序数据库。这里主要关注它的无损编码算法。基本思想就是采用二阶差分和异或操作对时序数据进行编码。
1.2.2 原理
Gorilla中每个时序数据（time series）由64bit的整型时间戳（time stamp）和双精度的浮点数数值（measurement）组成。利用时序数据在时间轴上具备的较强相关性，可以实现较好的数据压缩效果。
针对整型时间戳和浮点数类型的值，Gorilla采用了不同的压缩算法。
[图片]
1.2.3 适用数据类型
INT32, INT64, FLOAT, DOUBLE
1.2.4 适用数据模式
大部分的时序数据点相对于其邻近点没有显著的差异，即相邻的时序数据相似度很大。
但需要注意的是，当查询的时间范围较小时，由于值的解压缩可能依赖前值，通常需要读取更多的数据以计算出期望的时间段的时序数据。2. 压缩算法
2.1 GZIP压缩算法
2.1.1 简介
gzip是一种无损压缩算法，其基础为Deflate，Deflate是LZ77与哈弗曼编码的一个组合体。它的基本原理是：对于要压缩的文件，首先使用LZ77算法的一个变种进行压缩，对得到的结果再使用哈夫曼编码（根据情况，使用静态哈弗曼编码或动态哈夫曼编码）的方法进行压缩。
2.1.2 原理
2.2 LZ77算法
LZ77的核心思路是如果一个串中有两个重复的串，那么只需要知道后面的串与前面串重复的长度和后面串起始字符与前面串起始字符的距离。
例如：ABCCDEFABCCDEGH 通过LZ77算法可压缩为ABCCDEF(7,6)GH，其中7表示重复串起始字符A到前面串起始字符的距离，6表示重复部分的长度(ABCCDE)。
LZ77采用滑动窗口(sliding-windowcompression)来实现这个算法，扫描头从串的头部开始扫描，在扫描头的前面有一个长度未N的滑动窗口。若发现扫描头处的串和窗口里的最长匹配串是相同的，则用（两个串之间的距离， 串长度）来代替后一个重复的串，同时还需要添加一个表示是真实串还是替换后的串的字节在前面以方便解压。实际过程中滑动窗口的大小固定，匹配的串也有最小长度的限制，以方便（标识+串之间距离+串长度）之和所占用的字节是固定的。
3.6.3 哈夫曼编码
哈夫曼编码使用变长编码表对源符号进行编码，变长编码表通过评估源符号出现的频率得到，出现频率较高的字母使用较短的编码，反之出现频率低的使用较长的编码，这样使编码之后的字符串的平均长度、期望值降低，从而达到无损压缩数据的目的。
通过构造Huffman Tree的方式给字符重新编码，避免一个叶子的路径是另一个叶子路径的前缀，以保证出现频率越高的字符占用的字节越少。
例如：给英文单字“FO R G E T”进行哈弗曼编码，将每个英文字母出现频率由小排到大，如下图。
[图片]
每个字母与都代表一个终端节点（叶子节点），比较六个字母中每个字母出现的频率，将最小的两个字母相加合成一个新的节点。如下图。组成哈弗曼树。
[图片]
将上图给定的哈弗曼树的所有左边设为0，右边设为1，从根节点到叶子节点依次记录每个字母的编码，所得每个字母的编码表如下图。
[图片]

2.3 LZ4压缩算法
2.3.1 简介
LZ4是一种无损压缩算法，其目标是在压缩速度和压缩比之间提供一个良好的平衡。LZ4压缩速率在单个CPU核能超过 500 MB/s, 并且可以在多核之间扩展。解压速率更快，每个CPU核心的速度为多个GB/s，通常在多核系统上达到RAM速度限制。速度可以动态调整，选择“加速”因子来权衡压缩比和更快的速度。另一方面，也提供了高压缩衍生版本LZ4_HC，用CPU时间换取更好的压缩比。所有版本都具有相同的解压缩速度。
LZ4库使用BSD 2-Clause开源协议。
2.3.2 原理
LZ4算法的基本原理是让程序观察当前看到的数据是否和之前有重复，如果有，则用偏移量与匹配长度来记录，代替原始数据。
[图片]
[图片]
LZ4算法将数据表示为一系列序列(Sequence)。每个序列都以一个一字节的令牌(Token)开始，该令牌被分成两个4位字段。 第一个字段表示要复制到输出中的文字字节数。 第二个字段表示要从已解码的输出缓冲区中复制的字节数（其中0表示最小匹配长度为4个字节）。这两个位域中的15值表示长度更大，还有一个额外的数据字节要添加到长度中。这些额外字节中的255值表示还要添加另一个字节。因此，任意长度由一系列包含255值的额外字节表示。文字字符串在令牌和任何需要指示字符串长度的额外字节之后。其后是一个偏移量，指示在输出缓冲区中向后复制多远。匹配长度的额外字节（如果有）位于序列末尾。 压缩可以以流或块进行。
通过投入更多计算量寻找最佳匹配，可以实现更高的压缩比。这既减小了输出大小，又加快了解压速度。
Linear small-integer code
[图片]

2.3.3 适用数据类型
直接对字节进行编码，适用于INT32, INT64, FLOAT, DOUBLE, TEXT所有数据类型
2.3.4 适用数据模式
局部重复性较多的数据，完全随机的数据压缩率很低。

参数汇总 @Calloe
Java 版参数

含义
@谷新豪

Java版类型及取值范围

默认值

是否影响libtsfile
是否已实现

C++版本参数名称

C++类型及取值范围
默认值

目前C++版位置
RLE_MIN_REPEATED_NUM
RLE 算法中的最小重复次数。当某个数值连续重复出现的次数达到这个值时，RLE 算法开始将这些连续的重复数据编码为重复次数和数值的组合
[0, 2^31 - 1]
8

N
N
无

无
无
无
RLE_MAX_REPEATED_NUM
RLE 算法中的最大重复次数。当连续重复出现的次数超过这个值时，RLE 算法可能会选择其他的编码方式
[0, 2^31 - 1]
0x7FFF
N
N
无

无
无
无
RLE_MAX_BIT_PACKED_NUM

RLE 算法中的最大连续非重复数值个数。当连续非重复数值的个数不超过这个值时，RLE 算法可以选择将这些数值进行位打包编码
[0, 2^31 - 1]
63
N
N
无

无
无
无
FLOAT_VALUE_LENGTH

6
Y

4
src/common/db_common.h
get_data_type_size（）
DOUBLE_VALUE_LENGTH

7
Y

8
src/common/db_common.h
get_data_type_size（）
VALUE_BITS_LENGTH_32BIT

32
Y
Y
VALUE_BITS_LENGTH_32BIT

32
storage/tsfile/encode/gorilla_encoder.h
LEADING_ZERO_BITS_LENGTH_32BIT

5
Y
Y
LEADING_ZERO_BITS_LENGTH_32BIT

5
storage/tsfile/encode/gorilla_encoder.h
MEANINGFUL_XOR_BITS_LENGTH_32BIT

5
Y
Y
MEANINGFUL_XOR_BITS_LENGTH_32BIT

5
storage/tsfile/encode/gorilla_encoder.h
VALUE_BITS_LENGTH_64BIT

64
Y
Y
VALUE_BITS_LENGTH_64BIT

64
storage/tsfile/encode/gorilla_encoder.h
LEADING_ZERO_BITS_LENGTH_64BIT

6
Y
Y
LEADING_ZERO_BITS_LENGTH_64BIT

6
storage/tsfile/encode/gorilla_encoder.h
MEANINGFUL_XOR_BITS_LENGTH_64BIT

6
Y
Y
MEANINGFUL_XOR_BITS_LENGTH_64BIT

6
storage/tsfile/encode/gorilla_encoder.h
GORILLA_ENCODING_ENDING_INTEGER

Integer.MIN_VALUE
Y
Y
GORILLA_ENCODING_ENDING_INTEGER

INT32_MIN
storage/tsfile/encode/gorilla_encoder.h
GORILLA_ENCODING_ENDING_LONG

Long.MIN_VALUE
Y
Y
GORILLA_ENCODING_ENDING_LONG

INT64_MIN
storage/tsfile/encode/gorilla_encoder.h
GORILLA_ENCODING_ENDING_FLOAT

Float.NaN
Y
Y
GORILLA_ENCODING_ENDING_FLOAT

nanf("")
storage/tsfile/encode/gorilla_encoder.h
GORILLA_ENCODING_ENDING_DOUBLE

Double.NaN
Y
Y
GORILLA_ENCODING_ENDING_DOUBLE

nan("")
storage/tsfile/encode/gorilla_encoder.h
BYTE_SIZE_PER_CHAR

4
Y

STRING_ENCODING

"UTF-8"
Y

STRING_CHARSET

Charset.forName(STRING_ENCODING)
Y

CONFIG_FILE_NAME

"iotdb-common.properties"
N

MAGIC_STRING

"TsFile"
Y
Y
\*MAGIC_STRING_TSFILE

"TsFile"
storage/tsfile/tsfile_common.cc
VERSION_NUMBER_V2

"000002"
N

VERSION_NUMBER_V1

"000001"
N

VERSION_NUMBER

0x03
Y
Y
VERSION_NUM_BYTE

0x03
storage/tsfile/tsfile_common.cc
MIN_BLOOM_FILTER_ERROR_RATE
Bloom filter错误率最小值

0.01
Y
Y
MIN_BF_ERROR_RATE

0.01
storage/tsfile/bloom_filter.cc
MAX_BLOOM_FILTER_ERROR_RATE
Bloom filter错误率最大值

0.1
Y
Y
MAX_BF_ERROR_RATE

0.1
storage/tsfile/bloom_filter.cc
ARRAY_CAPACITY_THRESHOLD
Array 容量阈值

1000
Y
Y
ARRAY_INIT_CAPACITY

1000
common/container/array.h
groupSizeInByte
MemTable满了刷盘

128 _ 1024 _ 1024
N

pageSizeInByte
page大小

64\*1024
Y
Y
WRITE_STREAM_PAGE_SIZE

512
storage/tsfile/tsfile_io_writer.h

maxNumberOfPointsInPage
一个page最多多少个测点

10000
Y

page*writer_max_point_num*

5
common/global.cc init_config_value()

maxDegreeOfIndexNode

索引节点最大扇出度

256
Y

max*degree_of_index_node*

256
common/global.cc init_config_value()
timeSeriesDataType
时间戳列的数据类型，默认

INT64
Y

g*time_column_desc.type*

INT64
common/global.cc init_common()
maxStringLength
输入String的最大长度

128

N

floatPrecision

浮点数精度，默认是2表示保留小数点后2位，某些编码算法中会先将浮点数据转为整数，转换方法就是10进制左移floatPrecision位（左移2相当于乘100）。

2
参数迁移
Y

N

timeEncoding
时间列的编码方式

TS*2DIFF
Y
Y
time_encoding_type*

TS_2DIFF
common/global.cc init_config_value()
valueEncoder

value列默认编码方式，一般会要求用户手动指定一个编码方式

PLAIN

PLAIN
src/common/db_common.h
get_default_encoding_for_type()
rleBitWidth【去掉】
RLE参数

8
Y
Y
bit*width*

？？通过计算得到
storage/tsfile/encode/bitpack_encoder.h
deltaBlockSize

TS_2DIFF中的2diff 块大小

128
Y
Y
block*size*

128
/storage/tsfile/encode/ts2diff_encoder.h
freqType

"SINGLE_FREQ"
N

plaMaxError
?

100
N
N

sdtMaxError

100
N

dftSatisfyRate
？

0.1
N
N

freqEncodingSNR

40
N

freqEncodingBlockSize

1024
N

compressor

默认压缩方式

SNAPPY
Y

UNCOMPRESSED
src/common/db_common.h
get_default_compression_for_type（）
pageCheckSizeThreshold
检查页内存占用大小的行数阈值

100
Y
N

endian
统一大端

"BIG_ENDIAN"
N
N

TSFileStorageFs
去掉

FSType.LOCAL

coreSitePath
去掉

"/etc/hadoop/conf/core-site.xml"

hdfsSitePath
去掉

"/etc/hadoop/conf/hdfs-site.xml"

hdfsIp
去掉

"localhost"

hdfsPort
去掉

"9000"

dfsNameServices
去掉

"hdfsnamespace"

dfsHaNamenodes
去掉

"nn1,nn2"

dfsHaAutomaticFailoverEnabled
去掉

true

dfsClientFailoverProxyProvider
去掉

"org.apache.hadoop.hdfs.server.namenode.ha.ConfiguredFailoverProxyProvider"

useKerberos
去掉

false

kerberosKeytabFilePath
去掉

"/path"

kerberosPrincipal
去掉

"principal"

bloomFilterErrorRate

0.05
Y

tsfile*index_bloom_filter_error_percent*

0.05
common/global.cc
batchSize【已废弃】

数据迭代一次大小

1000
N

row*count*???

src/common/tsblock/tsblock.h get_row_count()??
maxTsBlockSizeInBytes
tsblock容量大小

128K
N

tsblock*max_memory*

512k
common/global.cc
maxTsBlockLineNumber
tsblock最多多少行

1000
N

max*row_count*

src/common/tsblock/tsblock.h
get_max_row_count()
patternMatchingThreshold
去掉

1000000
N

备注

1. 参数FLOAT_VALUE_LENGTH 只在使用特定编码器SinglePrecisionEncoderV1的时候对Tsfile文件有影响。此编码器在C++版本并未实现。
2. 参数DOUBLE_VALUE_LENGTH 只在使用特定编码器DoublePrecisionEncoderV1的时候对Tsfile文件有影响。此编码器在C++版本并未实现。
