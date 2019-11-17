--------------------------------------------------------
--  File created - sobota-listopada-09-2019   
--------------------------------------------------------
--------------------------------------------------------
--  DDL for Table BLOG
--------------------------------------------------------

  CREATE TABLE "C##TESTER"."BLOG" 
   (	"BLOGID" NUMBER(*,0), 
	"NAME" NVARCHAR2(50), 
	"TITLE" NVARCHAR2(250), 
	"DESCRIPTION" NVARCHAR2(1024), 
	"OWNER" NVARCHAR2(20), 
	"CREATEDAT" DATE, 
	"MODIFIEDAT" DATE, 
	"MODIFIEDBY" VARCHAR2(20 BYTE)
   ) SEGMENT CREATION IMMEDIATE 
  PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255 
 NOCOMPRESS LOGGING
  STORAGE(INITIAL 65536 NEXT 1048576 MINEXTENTS 1 MAXEXTENTS 2147483645
  PCTINCREASE 0 FREELISTS 1 FREELIST GROUPS 1
  BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT)
  TABLESPACE "USERS" ;
REM INSERTING into C##TESTER.BLOG
SET DEFINE OFF;
Insert into C##TESTER.BLOG (BLOGID,NAME,TITLE,DESCRIPTION,OWNER,CREATEDAT,MODIFIEDAT,MODIFIEDBY) values ('1','functional-data-access-with-sqlfun','Functional data access with SqlFun','Designing functional-relational mapper with F#','jacenty',to_date('19/11/06','RR/MM/DD'),null,null);
--------------------------------------------------------
--  DDL for Procedure SP_ADD_BLOG
--------------------------------------------------------
set define off;

  CREATE OR REPLACE EDITIONABLE PROCEDURE "C##TESTER"."SP_ADD_BLOG" 
(
  BLOGID IN NUMBER 
, NAME IN VARCHAR2 
, TITLE IN VARCHAR2 
, DESCRIPTION IN VARCHAR2 
, OWNER IN VARCHAR2 
, CREATEDAT IN DATE 
) AS 
BEGIN
  INSERT INTO blog (blogid, name, title, description, owner, createdat)
  VALUES (blogid, name, title, description, owner, createdat);
END SP_ADD_BLOG;

/
--------------------------------------------------------
--  DDL for Procedure SP_GET_BLOG
--------------------------------------------------------
set define off;

  CREATE OR REPLACE EDITIONABLE PROCEDURE "C##TESTER"."SP_GET_BLOG" 
(
  P_BLOGID IN NUMBER 
, CURSOR_ OUT SYS_REFCURSOR 
) AS 
BEGIN
  OPEN cursor_ FOR
  SELECT
    "A1"."BLOGID"        "BLOGID",
    "A1"."NAME"          "NAME",
    "A1"."TITLE"         "TITLE",
    "A1"."DESCRIPTION"   "DESCRIPTION",
    "A1"."OWNER"         "OWNER",
    "A1"."CREATEDAT"     "CREATEDAT",
    "A1"."MODIFIEDAT"    "MODIFIEDAT",
    "A1"."MODIFIEDBY"    "MODIFIEDBY"
FROM
    "C##TESTER"."BLOG" "A1";
END SP_GET_BLOG;

/
--------------------------------------------------------
--  Constraints for Table BLOG
--------------------------------------------------------

  ALTER TABLE "C##TESTER"."BLOG" MODIFY ("BLOGID" NOT NULL ENABLE);
  ALTER TABLE "C##TESTER"."BLOG" MODIFY ("NAME" NOT NULL ENABLE);
  ALTER TABLE "C##TESTER"."BLOG" MODIFY ("TITLE" NOT NULL ENABLE);
  ALTER TABLE "C##TESTER"."BLOG" MODIFY ("DESCRIPTION" NOT NULL ENABLE);
  ALTER TABLE "C##TESTER"."BLOG" MODIFY ("OWNER" NOT NULL ENABLE);
  ALTER TABLE "C##TESTER"."BLOG" MODIFY ("CREATEDAT" NOT NULL ENABLE);
