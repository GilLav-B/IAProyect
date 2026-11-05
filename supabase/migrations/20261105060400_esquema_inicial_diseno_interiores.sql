-- =====================================================================
-- Esquema inicial - Plataforma de Diseno de Interiores Asistida por IA
-- Fuente: docs/db/schema.sql (generado por EF Core), estado final tras
-- las migraciones InitialCreate + ObjectSortOrder + FreeRotation.
-- Se conservan exactamente nombres de tablas, columnas, constraints e
-- indices porque el backend .NET (EF Core) depende de ellos.
-- Unidades: cm y pesos MXN.
-- =====================================================================

-- ---------------------------------------------------------------------
-- projects
-- ---------------------------------------------------------------------
create table if not exists public.projects (
    id character varying(64) not null,
    name character varying(200) not null,
    type character varying(20) not null,
    created_at timestamp with time zone not null default (now()),
    constraint pk_projects primary key (id),
    constraint ck_projects_type check (type in ('comercial', 'domestico'))
);

-- ---------------------------------------------------------------------
-- providers
-- ---------------------------------------------------------------------
create table if not exists public.providers (
    id character varying(64) not null,
    name character varying(200) not null,
    budget_category character varying(20) not null,
    adapter character varying(10) not null,
    constraint pk_providers primary key (id),
    constraint ck_providers_adapter check (adapter in ('json', 'csv')),
    constraint ck_providers_budget_category check (budget_category in ('muebles', 'electronica', 'iluminacion', 'decoracion', 'materiales', 'instalacion'))
);

-- ---------------------------------------------------------------------
-- rooms
-- ---------------------------------------------------------------------
create table if not exists public.rooms (
    id character varying(64) not null,
    project_id character varying(64) not null,
    name character varying(200),
    space_type character varying(20) not null,
    length_cm integer not null,
    width_cm integer not null,
    height_cm integer not null,
    measurement_source character varying(20) not null default 'medidas_usuario',
    surfaces jsonb,
    commercial jsonb,
    constraint pk_rooms primary key (id),
    constraint ck_rooms_dimensions check (length_cm > 0 and width_cm > 0 and height_cm > 0),
    constraint ck_rooms_measurement_source check (measurement_source in ('medidas_usuario', 'planos', 'fotografias')),
    constraint ck_rooms_space_type check (space_type in ('sala', 'recamara', 'cocina', 'oficina', 'comedor')),
    constraint fk_rooms_projects_project_id foreign key (project_id) references public.projects (id) on delete cascade
);

-- ---------------------------------------------------------------------
-- materials
-- ---------------------------------------------------------------------
create table if not exists public.materials (
    id character varying(64) not null,
    type character varying(20) not null,
    name character varying(200) not null,
    price_per_m2_min numeric(12,2) not null,
    price_per_m2_max numeric(12,2) not null,
    color character varying(50),
    finish text,
    texture text,
    provider_id character varying(64),
    sku character varying(100),
    constraint pk_materials primary key (id),
    constraint ck_materials_price check (price_per_m2_min >= 0 and price_per_m2_max >= price_per_m2_min),
    constraint ck_materials_type check (type in ('madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio')),
    constraint fk_materials_providers_provider_id foreign key (provider_id) references public.providers (id) on delete restrict
);

-- ---------------------------------------------------------------------
-- products (catalogo: real | conceptual)
-- ---------------------------------------------------------------------
create table if not exists public.products (
    id character varying(64) not null,
    name character varying(200) not null,
    category character varying(30) not null,
    kind character varying(12) not null,
    width_cm integer not null,
    depth_cm integer not null,
    height_cm integer not null,
    materials text[] not null,
    colors text[] not null,
    styles text[] not null,
    price_min numeric(12,2) not null,
    price_max numeric(12,2) not null,
    attributes jsonb not null default ('{}'::jsonb),
    provider_id character varying(64),
    sku character varying(100),
    availability character varying(20),
    asset_3d text,
    images text[] not null,
    variants text[] not null,
    constraint pk_products primary key (id),
    constraint ck_products_availability check (availability is null or availability in ('disponible', 'agotado', 'sobre_pedido', 'desconocida')),
    constraint ck_products_category check (category in ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')),
    constraint ck_products_colors check (cardinality(colors) > 0),
    constraint ck_products_conceptual check (kind = 'real' or (provider_id is null and sku is null)),
    constraint ck_products_dimensions check (width_cm > 0 and depth_cm > 0 and height_cm > 0),
    constraint ck_products_kind check (kind in ('real', 'conceptual')),
    constraint ck_products_materials check (cardinality(materials) > 0 and materials <@ array['madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio']::text[]),
    constraint ck_products_price check (price_min >= 0 and price_max >= price_min),
    constraint ck_products_styles check (cardinality(styles) > 0 and styles <@ array['moderno', 'minimalista', 'industrial', 'japandi', 'clasico']::text[]),
    constraint fk_products_providers_provider_id foreign key (provider_id) references public.providers (id) on delete restrict
);

-- ---------------------------------------------------------------------
-- designs (versiones; parent_id = version de la que se ramifico)
-- ---------------------------------------------------------------------
create table if not exists public.designs (
    id character varying(64) not null,
    project_id character varying(64) not null,
    room_id character varying(64) not null,
    name character varying(200) not null,
    parent_id character varying(64),
    style character varying(20) not null,
    budget_mode character varying(20) not null,
    revision integer not null,
    author character varying(200),
    created_at timestamp with time zone not null default (now()),
    updated_at timestamp with time zone not null default (now()),
    favorite boolean not null,
    comments text,
    thumbnail_url text,
    render_url text,
    installation_min numeric(12,2),
    installation_max numeric(12,2),
    lighting jsonb,
    structural_confirmations text[] not null,
    constraint pk_designs primary key (id),
    constraint ck_designs_budget_mode check (budget_mode in ('minimizar', 'ajustar', 'flexible')),
    constraint ck_designs_installation check ((installation_min is null and installation_max is null) or (installation_min >= 0 and installation_max >= installation_min)),
    constraint ck_designs_not_own_parent check (parent_id is null or parent_id <> id),
    constraint ck_designs_revision check (revision >= 0),
    constraint ck_designs_structural_confirmations check (structural_confirmations <@ array['muro', 'columna', 'puerta', 'ventana']::text[]),
    constraint ck_designs_style check (style in ('moderno', 'minimalista', 'industrial', 'japandi', 'clasico')),
    constraint fk_designs_designs_parent_id foreign key (parent_id) references public.designs (id) on delete set null,
    constraint fk_designs_projects_project_id foreign key (project_id) references public.projects (id) on delete cascade,
    constraint fk_designs_rooms_room_id foreign key (room_id) references public.rooms (id) on delete restrict
);

-- ---------------------------------------------------------------------
-- requirements (por habitacion, con prioridad)
-- ---------------------------------------------------------------------
create table if not exists public.requirements (
    room_id character varying(64) not null,
    id character varying(64) not null,
    priority character varying(20) not null,
    type character varying(20) not null,
    description text,
    amount numeric(12,2),
    category character varying(30),
    attribute character varying(50),
    value jsonb,
    element character varying(50),
    style character varying(20),
    color character varying(50),
    constraint pk_requirements primary key (room_id, id),
    constraint ck_requirements_category check (category is null or category in ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')),
    constraint ck_requirements_priority check (priority in ('obligatorio', 'importante', 'preferencia', 'opcional')),
    constraint ck_requirements_shape check ((type = 'presupuesto_max' and amount > 0) or (type = 'tiene' and category is not null and attribute is not null and value is not null) or (type = 'conservar' and element is not null) or (type = 'estilo' and style is not null) or (type = 'color' and category is not null and color is not null) or (type = 'categoria' and category is not null)),
    constraint ck_requirements_style check (style is null or style in ('moderno', 'minimalista', 'industrial', 'japandi', 'clasico')),
    constraint ck_requirements_type check (type in ('presupuesto_max', 'tiene', 'conservar', 'estilo', 'color', 'categoria')),
    constraint fk_requirements_rooms_room_id foreign key (room_id) references public.rooms (id) on delete cascade
);

-- ---------------------------------------------------------------------
-- room_columns / room_doors / room_windows (elementos estructurales)
-- ---------------------------------------------------------------------
create table if not exists public.room_columns (
    room_id character varying(64) not null,
    id character varying(64) not null,
    x_cm integer not null,
    y_cm integer not null,
    width_cm integer not null,
    depth_cm integer not null,
    constraint pk_room_columns primary key (room_id, id),
    constraint ck_room_columns_size check (x_cm >= 0 and y_cm >= 0 and width_cm > 0 and depth_cm > 0),
    constraint fk_room_columns_rooms_room_id foreign key (room_id) references public.rooms (id) on delete cascade
);

create table if not exists public.room_doors (
    room_id character varying(64) not null,
    id character varying(64) not null,
    wall character varying(10) not null,
    position_cm integer not null,
    width_cm integer not null,
    constraint pk_room_doors primary key (room_id, id),
    constraint ck_room_doors_size check (position_cm >= 0 and width_cm > 0),
    constraint ck_room_doors_wall check (wall in ('norte', 'sur', 'este', 'oeste')),
    constraint fk_room_doors_rooms_room_id foreign key (room_id) references public.rooms (id) on delete cascade
);

create table if not exists public.room_windows (
    room_id character varying(64) not null,
    id character varying(64) not null,
    wall character varying(10) not null,
    position_cm integer not null,
    width_cm integer not null,
    sill_height_cm integer not null,
    height_cm integer,
    constraint pk_room_windows primary key (room_id, id),
    constraint ck_room_windows_size check (position_cm >= 0 and width_cm > 0 and sill_height_cm >= 0 and (height_cm is null or height_cm > 0)),
    constraint ck_room_windows_wall check (wall in ('norte', 'sur', 'este', 'oeste')),
    constraint fk_room_windows_rooms_room_id foreign key (room_id) references public.rooms (id) on delete cascade
);

-- ---------------------------------------------------------------------
-- design_history (historial / revertir)
-- ---------------------------------------------------------------------
create table if not exists public.design_history (
    id bigint generated always as identity,
    design_id character varying(64) not null,
    date timestamp with time zone not null default (now()),
    author character varying(200),
    description text not null,
    changed_instance_ids text[] not null,
    revision integer,
    snapshot jsonb,
    constraint pk_design_history primary key (id),
    constraint ck_design_history_revision check (revision is null or revision >= 0),
    constraint fk_design_history_designs_design_id foreign key (design_id) references public.designs (id) on delete cascade
);

-- ---------------------------------------------------------------------
-- design_materials
-- ---------------------------------------------------------------------
create table if not exists public.design_materials (
    id bigint generated always as identity,
    design_id character varying(64) not null,
    material_id character varying(64) not null,
    area_m2 numeric(10,2) not null,
    surface character varying(10),
    constraint pk_design_materials primary key (id),
    constraint ck_design_materials_area check (area_m2 > 0),
    constraint ck_design_materials_surface check (surface is null or surface in ('piso', 'muros', 'techo')),
    constraint fk_design_materials_designs_design_id foreign key (design_id) references public.designs (id) on delete cascade,
    constraint fk_design_materials_materials_material_id foreign key (material_id) references public.materials (id) on delete restrict
);

-- ---------------------------------------------------------------------
-- design_objects (instanceId unico por diseno)
-- Incluye sort_order (migracion EF ObjectSortOrder) y rotacion libre
-- 0-359 (migracion EF FreeRotation).
-- ---------------------------------------------------------------------
create table if not exists public.design_objects (
    design_id character varying(64) not null,
    instance_id character varying(64) not null,
    catalog_id character varying(64) not null,
    type character varying(30) not null,
    x_cm integer not null,
    y_cm integer not null,
    z_cm integer not null,
    rotation integer not null,
    scale_x double precision not null default 1.0,
    scale_y double precision not null default 1.0,
    scale_z double precision not null default 1.0,
    color character varying(50),
    material character varying(20),
    locked boolean not null,
    sort_order integer not null default 0,
    constraint pk_design_objects primary key (design_id, instance_id),
    constraint ck_design_objects_material check (material is null or material in ('madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio')),
    constraint ck_design_objects_rotation check (rotation >= 0 and rotation <= 359),
    constraint ck_design_objects_scale check (scale_x > 0 and scale_y > 0 and scale_z > 0),
    constraint ck_design_objects_type check (type in ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')),
    constraint ck_design_objects_z check (z_cm >= 0),
    constraint fk_design_objects_designs_design_id foreign key (design_id) references public.designs (id) on delete cascade,
    constraint fk_design_objects_products_catalog_id foreign key (catalog_id) references public.products (id) on delete restrict
);

-- ---------------------------------------------------------------------
-- Indices
-- ---------------------------------------------------------------------
create index if not exists ix_design_history_design_id_date on public.design_history (design_id, date);
create index if not exists ix_design_materials_design_id on public.design_materials (design_id);
create index if not exists ix_design_materials_material_id on public.design_materials (material_id);
create index if not exists ix_design_objects_catalog_id on public.design_objects (catalog_id);
create index if not exists ix_designs_parent_id on public.designs (parent_id);
create index if not exists ix_designs_project_id on public.designs (project_id);
create index if not exists ix_designs_room_id on public.designs (room_id);
create index if not exists ix_materials_provider_id on public.materials (provider_id);
create index if not exists ix_products_category on public.products (category);
create index if not exists ix_products_provider_id on public.products (provider_id);
create index if not exists ix_products_styles on public.products using gin (styles);
create index if not exists ix_rooms_project_id on public.rooms (project_id);

-- ---------------------------------------------------------------------
-- Row Level Security
-- Se activa RLS en todas las tablas de public y NO se crean politicas
-- para los roles anon/authenticated: con RLS activo y sin politicas,
-- la API publica de Supabase (PostgREST con la anon key o un JWT de
-- usuario) no puede leer ni escribir nada. El backend .NET se conecta
-- con el rol postgres (o service_role), que tiene BYPASSRLS, por lo que
-- no se ve afectado. Si en el futuro el frontend consulta Supabase
-- directamente, habra que agregar politicas explicitas.
-- ---------------------------------------------------------------------
alter table public.projects         enable row level security;
alter table public.providers        enable row level security;
alter table public.rooms            enable row level security;
alter table public.materials        enable row level security;
alter table public.products         enable row level security;
alter table public.designs          enable row level security;
alter table public.requirements     enable row level security;
alter table public.room_columns     enable row level security;
alter table public.room_doors       enable row level security;
alter table public.room_windows     enable row level security;
alter table public.design_history   enable row level security;
alter table public.design_materials enable row level security;
alter table public.design_objects   enable row level security;
