# Implementation of a simple graph query language with in memory data store

Select whole graph:
```
SELECT *
```

Select a particular vertex:
```
SELECT * WHERE id = '396d78df-a6bf-40cf-812e-6537cd1ff5de'
```

Select one department:
```
SELECT * WHERE department = "Marketing"
```

Select all directors (director, director of operations, etc.):
```
SELECT * WHERE position LIKE '%director%'
```

Limit result to specyfic vertex props, and only two types of edges:
```
SELECT name, position, department, 
edge.cooperation, edge.[knowledge sharing]
WHERE department = "Marketing"
```

Conditions can be combined with AND and OR operators. This query will return specific vertex props props and only two types of edges limiting results to only directors from two departments (IT or Marketing)
```
SELECT name, position, department, 
edge.cooperation, edge.[knowledge sharing]
WHERE (department = "IT" OR department = "Marketing")
AND position LIKE '%directors%'
```

It is possible to query nodes according to their connectivity.

For instance you can query all neighbours of some vertex (including the vertex itself)
```
SELECT * WHERE edge(source.id = '396d78df-a6bf-40cf-812e-6537cd1ff5de')
```

The query below will return all employees that are cooperating with "Marketing" department. The keyword edge matches all incoming and outgoing edges so this will include nodes on both ends of the replationships.
```
SELECT * WHERE edge(Name="Cooperation" AND Source.Dept = "Marketing")
```
On the other hand keyword in_edge matches only incoming edges. So Hence the query below will return only people connected to Marketing department but without the Marketing department itself (unless the Marketing department is internaly connected what is usually the case)
```
SELECT * WHERE in_edge(Source.Dept = 'Marketing')
```

If we'd like to explicitly exclude the Marketing department by adding an extra condition.
```
SELECT * WHERE in_edge(Source.Dept = 'Marketing') AND Dept != 'Marketing'
```

We can also limit results to people that often reach out for knowledge witn out_edge keyword, that matches only outgoing relationships
```
SELECT * WHERE out_edge(Name = 'Knowledge' AND Frequency > 3)
```

The last edge traversal operator is mutual_edge and it matches nodes connected with mutual relationship.
```
SELECT * WHERE mutual_edge(name='Cooperation')
```

Query language supports calculating basic graph metrics such as:
```
degree[(edge_condition)]
in_degree[(edge_condition)]
out_degree[(edge_condition)]
eigenvector[(edge_condition, edge_dist_prop)]
betweeness[(edge_condition, edge_dist_prop)]
path_length(target_condition, [edge_condition], [path_dist_prop])
shortest_path(source_condition, target_condition)
```

A simple query that will calculate indegree looks as follows
```
SELECT * CALCULATE indegree
```

A query that will calculate degree for a particular edge and out_degree for all edges
```
SELECT * CALCULATE degree(Name = "Cooperation"), out_degree
```

This query will return a shortest path between two nodes a and b the result will contain all vertices and edges along the path sorted accoring to order in the path
```
SELECT * CALCULATE shortest_path(Id='a', Id='b')
```

SELECT * CALCULATE js('graph.data = 1')