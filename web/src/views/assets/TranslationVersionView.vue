<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getTranslationVersion } from '@/api/assets'
import { ApiError } from '@/api/client'
import ErrorState from '@/components/ErrorState.vue'
import LoadingState from '@/components/LoadingState.vue'

const route = useRoute()
const router = useRouter()
const loading = ref(true)
const error = ref<ApiError | null>(null)

onMounted(loadVersion)

async function loadVersion() {
  loading.value = true
  error.value = null

  try {
    const versionId = Number(route.params.translationVersionId)
    const version = await getTranslationVersion(versionId)
    await router.replace({
      path: `/stories/${version.storyId}`,
      query: {
        translation_version_id: String(version.id),
      },
    })
  }
  catch (loadError) {
    error.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '译文版本加载失败，请稍后重试。')
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <LoadingState v-if="loading" label="加载译文版本" />
  <ErrorState v-else-if="error" :error="error" @retry="loadVersion" />
</template>
