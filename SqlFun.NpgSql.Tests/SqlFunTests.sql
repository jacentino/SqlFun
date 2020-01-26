--
-- PostgreSQL database dump
--

-- Dumped from database version 9.6.1
-- Dumped by pg_dump version 9.6.1

-- Started on 2017-06-19 22:34:26

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SET check_function_bodies = false;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 2168 (class 1262 OID 16739)
-- Name: SqlFunTests; Type: DATABASE; Schema: -; Owner: postgres
--

CREATE DATABASE "SqlFunTests" WITH TEMPLATE = template0 ENCODING = 'UTF8' LC_COLLATE = 'Polish_Poland.1250' LC_CTYPE = 'Polish_Poland.1250';


ALTER DATABASE "SqlFunTests" OWNER TO postgres;

\connect "SqlFunTests"

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SET check_function_bodies = false;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 1 (class 3079 OID 12387)
-- Name: plpgsql; Type: EXTENSION; Schema: -; Owner: 
--

CREATE EXTENSION IF NOT EXISTS plpgsql WITH SCHEMA pg_catalog;


--
-- TOC entry 2170 (class 0 OID 0)
-- Dependencies: 1
-- Name: EXTENSION plpgsql; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION plpgsql IS 'PL/pgSQL procedural language';


SET search_path = public, pg_catalog;

--
-- TOC entry 192 (class 1255 OID 16810)
-- Name: getblog(integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION getblog(p_id integer) RETURNS TABLE(blogid integer, name character varying, title character varying, description character varying, owner character varying, createdat timestamp without time zone, modifiedby character varying, modifiedat timestamp without time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
 RETURN QUERY select b.blogid, b.name, b.title, b.description, b.owner, b.createdAt, b.modifiedBy, b.modifiedAt 
	      from blog b
	      where b.blogid = p_id;
END; $$;


ALTER FUNCTION public.getblog(p_id integer) OWNER TO postgres;

SET default_tablespace = '';

SET default_with_oids = false;

--
-- TOC entry 186 (class 1259 OID 16743)
-- Name: blog; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE blog (
    blogid integer NOT NULL,
    name character varying(50) NOT NULL,
    title character varying(250) NOT NULL,
    description character varying(8000) NOT NULL,
    owner character varying(20) NOT NULL,
    createdat timestamp without time zone NOT NULL,
    modifiedat timestamp without time zone,
    modifiedby character varying(20)
);


ALTER TABLE blog OWNER TO postgres;

--
-- TOC entry 185 (class 1259 OID 16741)
-- Name: blog_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE blog_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE blog_id_seq OWNER TO postgres;

--
-- TOC entry 2171 (class 0 OID 0)
-- Dependencies: 185
-- Name: blog_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE blog_id_seq OWNED BY blog.blogid;


--
-- TOC entry 188 (class 1259 OID 16756)
-- Name: comment; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE comment (
    commentid integer NOT NULL,
    postid integer NOT NULL,
    parentid integer,
    content character varying(8000) NOT NULL,
    author character varying(20) NOT NULL,
    createdat timestamp without time zone
);


ALTER TABLE comment OWNER TO postgres;

--
-- TOC entry 187 (class 1259 OID 16754)
-- Name: comment_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE comment_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE comment_id_seq OWNER TO postgres;

--
-- TOC entry 2172 (class 0 OID 0)
-- Dependencies: 187
-- Name: comment_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE comment_id_seq OWNED BY comment.commentid;


--
-- TOC entry 190 (class 1259 OID 16767)
-- Name: post; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE post (
    postid integer NOT NULL,
    blogid integer NOT NULL,
    name character varying(50) NOT NULL,
    title character varying(250) NOT NULL,
    content character varying(8000) NOT NULL,
    author character varying(20) NOT NULL,
    createdat timestamp without time zone NOT NULL,
    modifiedat timestamp without time zone,
    modifiedby character varying(20),
    status character(1) NOT NULL
);


ALTER TABLE post OWNER TO postgres;

CREATE TABLE userprofile
(
  id character(20) NOT NULL,
  name character varying(80) NOT NULL,
  email character varying(200),
  avatar bytea NOT NULL,
  CONSTRAINT userprofile_pkey PRIMARY KEY (id)
);

ALTER TABLE public.userprofile
  OWNER TO postgres;
--
-- TOC entry 189 (class 1259 OID 16765)
-- Name: post_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE post_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE post_id_seq OWNER TO postgres;

--
-- TOC entry 2173 (class 0 OID 0)
-- Dependencies: 189
-- Name: post_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE post_id_seq OWNED BY post.postid;


--
-- TOC entry 191 (class 1259 OID 16778)
-- Name: tag; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE tag (
    postid integer NOT NULL,
    name character varying(50) NOT NULL
);


ALTER TABLE tag OWNER TO postgres;

--
-- TOC entry 2021 (class 2604 OID 16746)
-- Name: blog blogid; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY blog ALTER COLUMN blogid SET DEFAULT nextval('blog_id_seq'::regclass);


--
-- TOC entry 2022 (class 2604 OID 16759)
-- Name: comment commentid; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY comment ALTER COLUMN commentid SET DEFAULT nextval('comment_id_seq'::regclass);


--
-- TOC entry 2023 (class 2604 OID 16770)
-- Name: post postid; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY post ALTER COLUMN postid SET DEFAULT nextval('post_id_seq'::regclass);


--
-- TOC entry 2158 (class 0 OID 16743)
-- Dependencies: 186
-- Data for Name: blog; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY blog (blogid, name, title, description, owner, createdat, modifiedat, modifiedby) FROM stdin;
1	functional-data-access-with-sqlfu	Functional data access with SqlFu	Designing functional-relational mapper with F#	jacentino	2017-06-01 00:00:00	\N	\N
\.


--
-- TOC entry 2174 (class 0 OID 0)
-- Dependencies: 185
-- Name: blog_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('blog_id_seq', 1, true);


--
-- TOC entry 2160 (class 0 OID 16756)
-- Dependencies: 188
-- Data for Name: comment; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY comment (commentid, postid, parentid, content, author, createdat) FROM stdin;
1	1	\N	Great, informative article!	joeblack	2017-06-01 00:00:00
2	1	1	Thank you!	jacenty	2017-06-01 00:00:00
3	1	2	You're welcome!	joeblack	2017-06-01 00:00:00
\.


--
-- TOC entry 2175 (class 0 OID 0)
-- Dependencies: 187
-- Name: comment_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('comment_id_seq', 3, true);


--
-- TOC entry 2162 (class 0 OID 16767)
-- Dependencies: 190
-- Data for Name: post; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY post (postid, blogid, name, title, content, author, createdat, modifiedat, modifiedby, status) FROM stdin;
1	1	another-sql-framework	Yet another sql framework	There are so many solutions for this problem. What is the case for another one?	jacenty	2017-06-01 00:00:00	\N	\N	P
2	1	whats-wrong-with-existing-f	What's wrong with existing frameworks	Shortly - they not align with functional paradigm.	jacenty	2017-06-01 00:00:00	\N	\N	P
\.

--
-- TOC entry 2176 (class 0 OID 0)
-- Dependencies: 189
-- Name: post_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('post_id_seq', 2, true);


--
-- TOC entry 2163 (class 0 OID 16778)
-- Dependencies: 191
-- Data for Name: tag; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY tag (postid, name) FROM stdin;
1	existing
1	framework
1	options
\.


--
-- TOC entry 2025 (class 2606 OID 16753)
-- Name: blog ix_blog_1; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY blog
    ADD CONSTRAINT ix_blog_1 UNIQUE (name);


--
-- TOC entry 2031 (class 2606 OID 16777)
-- Name: post ix_post; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY post
    ADD CONSTRAINT ix_post UNIQUE (blogid, name);


--
-- TOC entry 2027 (class 2606 OID 16751)
-- Name: blog pk_blog_1; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY blog
    ADD CONSTRAINT pk_blog_1 PRIMARY KEY (blogid);


--
-- TOC entry 2029 (class 2606 OID 16764)
-- Name: comment pk_comment; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY comment
    ADD CONSTRAINT pk_comment PRIMARY KEY (commentid);


--
-- TOC entry 2033 (class 2606 OID 16775)
-- Name: post pk_post; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY post
    ADD CONSTRAINT pk_post PRIMARY KEY (postid);


--
-- TOC entry 2035 (class 2606 OID 16782)
-- Name: tag pk_tag; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY tag
    ADD CONSTRAINT pk_tag PRIMARY KEY (postid, name);


--
-- TOC entry 2037 (class 2606 OID 16783)
-- Name: comment fk_comment_comment; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY comment
    ADD CONSTRAINT fk_comment_comment FOREIGN KEY (parentid) REFERENCES comment(commentid);


--
-- TOC entry 2036 (class 2606 OID 16788)
-- Name: comment fk_comment_post; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY comment
    ADD CONSTRAINT fk_comment_post FOREIGN KEY (postid) REFERENCES post(postid) ON DELETE CASCADE;


--
-- TOC entry 2038 (class 2606 OID 16793)
-- Name: post fk_post_blog; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY post
    ADD CONSTRAINT fk_post_blog FOREIGN KEY (blogid) REFERENCES blog(blogid) ON DELETE CASCADE;


--
-- TOC entry 2039 (class 2606 OID 16798)
-- Name: tag fk_tag_post; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY tag
    ADD CONSTRAINT fk_tag_post FOREIGN KEY (postid) REFERENCES post(postid) ON DELETE CASCADE;


-- Completed on 2017-06-19 22:34:32

--
-- PostgreSQL database dump complete
--

