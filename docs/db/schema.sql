CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE projects (
        id character varying(64) NOT NULL,
        name character varying(200) NOT NULL,
        type character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_projects PRIMARY KEY (id),
        CONSTRAINT ck_projects_type CHECK (type IN ('comercial', 'domestico'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE providers (
        id character varying(64) NOT NULL,
        name character varying(200) NOT NULL,
        budget_category character varying(20) NOT NULL,
        adapter character varying(10) NOT NULL,
        CONSTRAINT pk_providers PRIMARY KEY (id),
        CONSTRAINT ck_providers_adapter CHECK (adapter IN ('json', 'csv')),
        CONSTRAINT ck_providers_budget_category CHECK (budget_category IN ('muebles', 'electronica', 'iluminacion', 'decoracion', 'materiales', 'instalacion'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE rooms (
        id character varying(64) NOT NULL,
        project_id character varying(64) NOT NULL,
        name character varying(200),
        space_type character varying(20) NOT NULL,
        length_cm integer NOT NULL,
        width_cm integer NOT NULL,
        height_cm integer NOT NULL,
        measurement_source character varying(20) NOT NULL DEFAULT 'medidas_usuario',
        surfaces jsonb,
        commercial jsonb,
        CONSTRAINT pk_rooms PRIMARY KEY (id),
        CONSTRAINT ck_rooms_dimensions CHECK (length_cm > 0 AND width_cm > 0 AND height_cm > 0),
        CONSTRAINT ck_rooms_measurement_source CHECK (measurement_source IN ('medidas_usuario', 'planos', 'fotografias')),
        CONSTRAINT ck_rooms_space_type CHECK (space_type IN ('sala', 'recamara', 'cocina', 'oficina', 'comedor')),
        CONSTRAINT fk_rooms_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE materials (
        id character varying(64) NOT NULL,
        type character varying(20) NOT NULL,
        name character varying(200) NOT NULL,
        price_per_m2_min numeric(12,2) NOT NULL,
        price_per_m2_max numeric(12,2) NOT NULL,
        color character varying(50),
        finish text,
        texture text,
        provider_id character varying(64),
        sku character varying(100),
        CONSTRAINT pk_materials PRIMARY KEY (id),
        CONSTRAINT ck_materials_price CHECK (price_per_m2_min >= 0 AND price_per_m2_max >= price_per_m2_min),
        CONSTRAINT ck_materials_type CHECK (type IN ('madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio')),
        CONSTRAINT fk_materials_providers_provider_id FOREIGN KEY (provider_id) REFERENCES providers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE products (
        id character varying(64) NOT NULL,
        name character varying(200) NOT NULL,
        category character varying(30) NOT NULL,
        kind character varying(12) NOT NULL,
        width_cm integer NOT NULL,
        depth_cm integer NOT NULL,
        height_cm integer NOT NULL,
        materials text[] NOT NULL,
        colors text[] NOT NULL,
        styles text[] NOT NULL,
        price_min numeric(12,2) NOT NULL,
        price_max numeric(12,2) NOT NULL,
        attributes jsonb NOT NULL DEFAULT ('{}'::jsonb),
        provider_id character varying(64),
        sku character varying(100),
        availability character varying(20),
        asset_3d text,
        images text[] NOT NULL,
        variants text[] NOT NULL,
        CONSTRAINT pk_products PRIMARY KEY (id),
        CONSTRAINT ck_products_availability CHECK (availability IS NULL OR availability IN ('disponible', 'agotado', 'sobre_pedido', 'desconocida')),
        CONSTRAINT ck_products_category CHECK (category IN ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')),
        CONSTRAINT ck_products_colors CHECK (cardinality(colors) > 0),
        CONSTRAINT ck_products_conceptual CHECK (kind = 'real' OR (provider_id IS NULL AND sku IS NULL)),
        CONSTRAINT ck_products_dimensions CHECK (width_cm > 0 AND depth_cm > 0 AND height_cm > 0),
        CONSTRAINT ck_products_kind CHECK (kind IN ('real', 'conceptual')),
        CONSTRAINT ck_products_materials CHECK (cardinality(materials) > 0 AND materials <@ ARRAY['madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio']::text[]),
        CONSTRAINT ck_products_price CHECK (price_min >= 0 AND price_max >= price_min),
        CONSTRAINT ck_products_styles CHECK (cardinality(styles) > 0 AND styles <@ ARRAY['moderno', 'minimalista', 'industrial', 'japandi', 'clasico']::text[]),
        CONSTRAINT fk_products_providers_provider_id FOREIGN KEY (provider_id) REFERENCES providers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE designs (
        id character varying(64) NOT NULL,
        project_id character varying(64) NOT NULL,
        room_id character varying(64) NOT NULL,
        name character varying(200) NOT NULL,
        parent_id character varying(64),
        style character varying(20) NOT NULL,
        budget_mode character varying(20) NOT NULL,
        revision integer NOT NULL,
        author character varying(200),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        favorite boolean NOT NULL,
        comments text,
        thumbnail_url text,
        render_url text,
        installation_min numeric(12,2),
        installation_max numeric(12,2),
        lighting jsonb,
        structural_confirmations text[] NOT NULL,
        CONSTRAINT pk_designs PRIMARY KEY (id),
        CONSTRAINT ck_designs_budget_mode CHECK (budget_mode IN ('minimizar', 'ajustar', 'flexible')),
        CONSTRAINT ck_designs_installation CHECK ((installation_min IS NULL AND installation_max IS NULL) OR (installation_min >= 0 AND installation_max >= installation_min)),
        CONSTRAINT ck_designs_not_own_parent CHECK (parent_id IS NULL OR parent_id <> id),
        CONSTRAINT ck_designs_revision CHECK (revision >= 0),
        CONSTRAINT ck_designs_structural_confirmations CHECK (structural_confirmations <@ ARRAY['muro', 'columna', 'puerta', 'ventana']::text[]),
        CONSTRAINT ck_designs_style CHECK (style IN ('moderno', 'minimalista', 'industrial', 'japandi', 'clasico')),
        CONSTRAINT fk_designs_designs_parent_id FOREIGN KEY (parent_id) REFERENCES designs (id) ON DELETE SET NULL,
        CONSTRAINT fk_designs_projects_project_id FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE,
        CONSTRAINT fk_designs_rooms_room_id FOREIGN KEY (room_id) REFERENCES rooms (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE requirements (
        room_id character varying(64) NOT NULL,
        id character varying(64) NOT NULL,
        priority character varying(20) NOT NULL,
        type character varying(20) NOT NULL,
        description text,
        amount numeric(12,2),
        category character varying(30),
        attribute character varying(50),
        value jsonb,
        element character varying(50),
        style character varying(20),
        color character varying(50),
        CONSTRAINT pk_requirements PRIMARY KEY (room_id, id),
        CONSTRAINT ck_requirements_category CHECK (category IS NULL OR category IN ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')),
        CONSTRAINT ck_requirements_priority CHECK (priority IN ('obligatorio', 'importante', 'preferencia', 'opcional')),
        CONSTRAINT ck_requirements_shape CHECK ((type = 'presupuesto_max' AND amount > 0) OR (type = 'tiene' AND category IS NOT NULL AND attribute IS NOT NULL AND value IS NOT NULL) OR (type = 'conservar' AND element IS NOT NULL) OR (type = 'estilo' AND style IS NOT NULL) OR (type = 'color' AND category IS NOT NULL AND color IS NOT NULL) OR (type = 'categoria' AND category IS NOT NULL)),
        CONSTRAINT ck_requirements_style CHECK (style IS NULL OR style IN ('moderno', 'minimalista', 'industrial', 'japandi', 'clasico')),
        CONSTRAINT ck_requirements_type CHECK (type IN ('presupuesto_max', 'tiene', 'conservar', 'estilo', 'color', 'categoria')),
        CONSTRAINT fk_requirements_rooms_room_id FOREIGN KEY (room_id) REFERENCES rooms (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE room_columns (
        room_id character varying(64) NOT NULL,
        id character varying(64) NOT NULL,
        x_cm integer NOT NULL,
        y_cm integer NOT NULL,
        width_cm integer NOT NULL,
        depth_cm integer NOT NULL,
        CONSTRAINT pk_room_columns PRIMARY KEY (room_id, id),
        CONSTRAINT ck_room_columns_size CHECK (x_cm >= 0 AND y_cm >= 0 AND width_cm > 0 AND depth_cm > 0),
        CONSTRAINT fk_room_columns_rooms_room_id FOREIGN KEY (room_id) REFERENCES rooms (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE room_doors (
        room_id character varying(64) NOT NULL,
        id character varying(64) NOT NULL,
        wall character varying(10) NOT NULL,
        position_cm integer NOT NULL,
        width_cm integer NOT NULL,
        CONSTRAINT pk_room_doors PRIMARY KEY (room_id, id),
        CONSTRAINT ck_room_doors_size CHECK (position_cm >= 0 AND width_cm > 0),
        CONSTRAINT ck_room_doors_wall CHECK (wall IN ('norte', 'sur', 'este', 'oeste')),
        CONSTRAINT fk_room_doors_rooms_room_id FOREIGN KEY (room_id) REFERENCES rooms (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE room_windows (
        room_id character varying(64) NOT NULL,
        id character varying(64) NOT NULL,
        wall character varying(10) NOT NULL,
        position_cm integer NOT NULL,
        width_cm integer NOT NULL,
        sill_height_cm integer NOT NULL,
        height_cm integer,
        CONSTRAINT pk_room_windows PRIMARY KEY (room_id, id),
        CONSTRAINT ck_room_windows_size CHECK (position_cm >= 0 AND width_cm > 0 AND sill_height_cm >= 0 AND (height_cm IS NULL OR height_cm > 0)),
        CONSTRAINT ck_room_windows_wall CHECK (wall IN ('norte', 'sur', 'este', 'oeste')),
        CONSTRAINT fk_room_windows_rooms_room_id FOREIGN KEY (room_id) REFERENCES rooms (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE design_history (
        id bigint GENERATED ALWAYS AS IDENTITY,
        design_id character varying(64) NOT NULL,
        date timestamp with time zone NOT NULL DEFAULT (now()),
        author character varying(200),
        description text NOT NULL,
        changed_instance_ids text[] NOT NULL,
        revision integer,
        snapshot jsonb,
        CONSTRAINT pk_design_history PRIMARY KEY (id),
        CONSTRAINT ck_design_history_revision CHECK (revision IS NULL OR revision >= 0),
        CONSTRAINT fk_design_history_designs_design_id FOREIGN KEY (design_id) REFERENCES designs (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE design_materials (
        id bigint GENERATED ALWAYS AS IDENTITY,
        design_id character varying(64) NOT NULL,
        material_id character varying(64) NOT NULL,
        area_m2 numeric(10,2) NOT NULL,
        surface character varying(10),
        CONSTRAINT pk_design_materials PRIMARY KEY (id),
        CONSTRAINT ck_design_materials_area CHECK (area_m2 > 0),
        CONSTRAINT ck_design_materials_surface CHECK (surface IS NULL OR surface IN ('piso', 'muros', 'techo')),
        CONSTRAINT fk_design_materials_designs_design_id FOREIGN KEY (design_id) REFERENCES designs (id) ON DELETE CASCADE,
        CONSTRAINT fk_design_materials_materials_material_id FOREIGN KEY (material_id) REFERENCES materials (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE TABLE design_objects (
        design_id character varying(64) NOT NULL,
        instance_id character varying(64) NOT NULL,
        catalog_id character varying(64) NOT NULL,
        type character varying(30) NOT NULL,
        x_cm integer NOT NULL,
        y_cm integer NOT NULL,
        z_cm integer NOT NULL,
        rotation integer NOT NULL,
        scale_x double precision NOT NULL DEFAULT 1.0,
        scale_y double precision NOT NULL DEFAULT 1.0,
        scale_z double precision NOT NULL DEFAULT 1.0,
        color character varying(50),
        material character varying(20),
        locked boolean NOT NULL,
        CONSTRAINT pk_design_objects PRIMARY KEY (design_id, instance_id),
        CONSTRAINT ck_design_objects_material CHECK (material IS NULL OR material IN ('madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio')),
        CONSTRAINT ck_design_objects_rotation CHECK (rotation IN (0, 90, 180, 270)),
        CONSTRAINT ck_design_objects_scale CHECK (scale_x > 0 AND scale_y > 0 AND scale_z > 0),
        CONSTRAINT ck_design_objects_type CHECK (type IN ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')),
        CONSTRAINT ck_design_objects_z CHECK (z_cm >= 0),
        CONSTRAINT fk_design_objects_designs_design_id FOREIGN KEY (design_id) REFERENCES designs (id) ON DELETE CASCADE,
        CONSTRAINT fk_design_objects_products_catalog_id FOREIGN KEY (catalog_id) REFERENCES products (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_design_history_design_id_date ON design_history (design_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_design_materials_design_id ON design_materials (design_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_design_materials_material_id ON design_materials (material_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_design_objects_catalog_id ON design_objects (catalog_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_designs_parent_id ON designs (parent_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_designs_project_id ON designs (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_designs_room_id ON designs (room_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_materials_provider_id ON materials (provider_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_products_category ON products (category);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_products_provider_id ON products (provider_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_products_styles ON products USING gin (styles);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    CREATE INDEX ix_rooms_project_id ON rooms (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103230409_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261103230409_InitialCreate', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261104002809_ObjectSortOrder') THEN
    ALTER TABLE design_objects ADD sort_order integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261104002809_ObjectSortOrder') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261104002809_ObjectSortOrder', '10.0.14');
    END IF;
END $EF$;
COMMIT;

