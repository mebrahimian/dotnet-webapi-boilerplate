resource "aws_ecs_cluster" "cluster" {
  name = "fsh-cluster"
  tags = merge(var.common_tags)
}

resource "aws_ecs_cluster_capacity_providers" "cluster" {
  cluster_name       = aws_ecs_cluster.cluster.name
  capacity_providers = ["FARGATE"]

  default_capacity_provider_strategy {
    base              = 1
    weight            = 100
    capacity_provider = "FARGATE"
  }
}

resource "aws_ecs_service" "api_ecs_service" {
  name                              = "webapi"
  cluster                           = aws_ecs_cluster.cluster.id
  task_definition                   = aws_ecs_task_definition.api_ecs_task.arn
  launch_type                       = "FARGATE"
  desired_count                     = 1
  tags                              = merge(var.common_tags)
  health_check_grace_period_seconds = 90

  load_balancer {
    target_group_arn = aws_lb_target_group.fsh_api_tg.arn
    container_name   = "webapi"
    container_port   = 80
  }
  network_configuration {
    subnets          = [aws_subnet.private_east_a.id, aws_subnet.private_east_b.id]
    security_groups  = [aws_security_group.lb.id]
    assign_public_ip = true
  }
}

resource "aws_ecs_task_definition" "api_ecs_task" {
  family                   = "webapi"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = 256
  memory                   = 512
  task_role_arn            = aws_iam_role.ecs_task_role.arn
  execution_role_arn       = aws_iam_role.ecs_task_execution_role.arn
  tags                     = merge(var.common_tags)
  container_definitions = jsonencode([
    {
      name : "webapi"
      image : "iammukeshm/dotnet-webapi:latest"
      cpu : 256
      memory : 512
      essential : true
      environment : [
        { "name" : "ASPNETCORE_ENVIRONMENT", "value" : "staging" },
        { "name" : "DatabaseSettings__ConnectionString", "value" : "Host=${aws_db_instance.postgres.endpoint};Port=5432;Database=fshdb;Username=${var.pg_username};Password=${var.pg_password};Include Error Detail=true" },
        { "name" : "DatabaseSettings__DBProvider", "value" : "postgresql" },
        { "name" : "HangfireSettings__Storage__ConnectionString", "value" : "Host=${aws_db_instance.postgres.endpoint};Port=5432;Database=fshdb;Username=${var.pg_username};Password=${var.pg_password};Include Error Detail=true" },
        { "name" : "HangfireSettings__Storage__StorageProvider", "value" : "postgresql" }
      ]
      logConfiguration : {
        "logDriver" : "awslogs",
        "options" : {
          "awslogs-region" : var.aws_region,
          "awslogs-group" : "fsh/dotnet-webapi",
          "awslogs-stream-prefix" : "fsh-api"
        }
      },
      portMappings : [
        {
          "containerPort" : 80,
          "hostPort" : 80
        }
      ]
    }
  ])
}

resource "aws_security_group" "lb" {
  name   = "security-group"
  vpc_id = aws_vpc.project_ecs.id
  tags   = merge(var.common_tags)
  ingress {
    protocol    = "tcp"
    from_port   = 80
    to_port     = 80
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_internet_gateway" "api" {
  vpc_id = aws_vpc.project_ecs.id
  tags   = merge(var.common_tags)
}

resource "aws_route" "internet_access" {
  route_table_id         = aws_vpc.project_ecs.main_route_table_id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.api.id
}

resource "aws_cloudwatch_log_group" "api_log_group" {
  name              = "fsh/dotnet-webapi"
  retention_in_days = 5
  tags              = merge(var.common_tags)
}