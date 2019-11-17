CREATE DATABASE `sqlfuntest` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;


CREATE TABLE `blog` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(50) NOT NULL,
  `title` varchar(250) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8 COLLATE utf8_general_ci DEFAULT NULL,
  `owner` varchar(20) NOT NULL,
  `createdat` datetime NOT NULL,
  `modifiedat` datetime DEFAULT NULL,
  `modifiedby` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id_UNIQUE` (`id`),
  UNIQUE KEY `name_UNIQUE` (`name`),
  UNIQUE KEY `title_UNIQUE` (`title`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `addblog`( id int
    , name varchar(50)
    , title varchar(250)
    , description varchar(1024)
    , owner varchar(20)
    , createdAt datetime
    )
BEGIN
	insert into blog (id, name, title, description, owner, createdAt) 
              values (id, name, title, description, owner, createdAt);
END$$
DELIMITER ;


DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `getblog`(blogid int)
BEGIN
	select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where id = blogid;
END$$
DELIMITER ;
